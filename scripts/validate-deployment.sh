#!/bin/bash

# Deployment Validation Script
# This script validates that the deployment was successful and the application is running correctly

set -e

# Configuration
ENVIRONMENT=${1:-"dev"}
TIMEOUT=300  # 5 minutes timeout
RETRY_INTERVAL=10  # 10 seconds between retries

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

# Function to check HTTP endpoint
check_endpoint() {
    local url=$1
    local expected_status=$2
    local description=$3
    
    print_status $YELLOW "Checking $description: $url"
    
    local status_code=$(curl -s -o /dev/null -w "%{http_code}" "$url" || echo "000")
    
    if [ "$status_code" = "$expected_status" ]; then
        print_status $GREEN "? $description: HTTP $status_code (Expected: $expected_status)"
        return 0
    else
        print_status $RED "? $description: HTTP $status_code (Expected: $expected_status)"
        return 1
    fi
}

# Function to wait for deployment to be ready
wait_for_deployment() {
    local url=$1
    local max_attempts=$((TIMEOUT / RETRY_INTERVAL))
    local attempt=1
    
    print_status $YELLOW "Waiting for deployment to be ready (timeout: ${TIMEOUT}s)..."
    
    while [ $attempt -le $max_attempts ]; do
        if check_endpoint "$url/health" "200" "Health Check" > /dev/null 2>&1; then
            print_status $GREEN "Deployment is ready after $((attempt * RETRY_INTERVAL)) seconds"
            return 0
        fi
        
        print_status $YELLOW "Attempt $attempt/$max_attempts failed, retrying in ${RETRY_INTERVAL}s..."
        sleep $RETRY_INTERVAL
        attempt=$((attempt + 1))
    done
    
    print_status $RED "Deployment failed to become ready within ${TIMEOUT} seconds"
    return 1
}

# Function to run comprehensive tests
run_tests() {
    local base_url=$1
    local failures=0
    
    print_status $YELLOW "Running deployment validation tests..."
    
    # Test 1: Health Check
    if ! check_endpoint "$base_url/health" "200" "Health Check"; then
        failures=$((failures + 1))
    fi
    
    # Test 2: Home Page
    if ! check_endpoint "$base_url" "200" "Home Page"; then
        failures=$((failures + 1))
    fi
    
    # Test 3: Stock Dashboard
    if ! check_endpoint "$base_url/Stock/Dashboard" "200" "Stock Dashboard"; then
        failures=$((failures + 1))
    fi
    
    # Test 4: Static Assets
    if ! check_endpoint "$base_url/js/stock-tracker.js" "200" "JavaScript Assets"; then
        failures=$((failures + 1))
    fi
    
    # Test 5: API Endpoint (should return JSON)
    print_status $YELLOW "Checking API endpoint: $base_url/Stock/GetSuggestions?query=AAPL"
    local api_response=$(curl -s "$base_url/Stock/GetSuggestions?query=AAPL" || echo "")
    if echo "$api_response" | grep -q "\["; then
        print_status $GREEN "? API Endpoint: Returns JSON response"
    else
        print_status $RED "? API Endpoint: Invalid response"
        failures=$((failures + 1))
    fi
    
    # Test 6: HTTPS Redirect (if not already HTTPS)
    if [[ $base_url == http://* ]]; then
        local https_url=${base_url/http:/https:}
        if ! check_endpoint "$https_url" "200" "HTTPS Redirect"; then
            failures=$((failures + 1))
        fi
    fi
    
    return $failures
}

# Function to check Azure resources
check_azure_resources() {
    local environment=$1
    
    print_status $YELLOW "Checking Azure resources for $environment environment..."
    
    # Set resource group based on environment
    if [ "$environment" = "prod" ]; then
        local rg_name="ai-stock-tracker-prod-rg"
        local app_name="ai-stock-tracker-prod-webapp"
    else
        local rg_name="ai-stock-tracker-dev-rg"
        local app_name="ai-stock-tracker-dev-webapp"
    fi
    
    # Check if Azure CLI is available
    if ! command -v az &> /dev/null; then
        print_status $YELLOW "Azure CLI not available, skipping Azure resource checks"
        return 0
    fi
    
    # Check resource group
    if az group show --name "$rg_name" &> /dev/null; then
        print_status $GREEN "? Resource Group: $rg_name exists"
    else
        print_status $RED "? Resource Group: $rg_name not found"
        return 1
    fi
    
    # Check web app
    if az webapp show --name "$app_name" --resource-group "$rg_name" &> /dev/null; then
        print_status $GREEN "? Web App: $app_name exists"
        
        # Check web app status
        local app_state=$(az webapp show --name "$app_name" --resource-group "$rg_name" --query "state" -o tsv)
        if [ "$app_state" = "Running" ]; then
            print_status $GREEN "? Web App Status: Running"
        else
            print_status $RED "? Web App Status: $app_state"
            return 1
        fi
    else
        print_status $RED "? Web App: $app_name not found"
        return 1
    fi
    
    return 0
}

# Main execution
main() {
    print_status $YELLOW "Starting deployment validation for $ENVIRONMENT environment"
    
    # Determine the base URL
    if [ "$ENVIRONMENT" = "prod" ]; then
        BASE_URL="https://ai-stock-tracker-prod-webapp.azurewebsites.net"
    else
        BASE_URL="https://ai-stock-tracker-dev-webapp.azurewebsites.net"
    fi
    
    print_status $YELLOW "Target URL: $BASE_URL"
    
    # Wait for deployment to be ready
    if ! wait_for_deployment "$BASE_URL"; then
        print_status $RED "Deployment validation failed: Application not ready"
        exit 1
    fi
    
    # Run comprehensive tests
    if ! run_tests "$BASE_URL"; then
        local test_failures=$?
        print_status $RED "Deployment validation failed: $test_failures test(s) failed"
        exit 1
    fi
    
    # Check Azure resources (if Azure CLI is available)
    if ! check_azure_resources "$ENVIRONMENT"; then
        print_status $RED "Deployment validation failed: Azure resource check failed"
        exit 1
    fi
    
    print_status $GREEN "?? Deployment validation successful!"
    print_status $GREEN "Application is running correctly at: $BASE_URL"
    
    # Output summary
    echo ""
    print_status $YELLOW "=== Deployment Summary ==="
    print_status $GREEN "Environment: $ENVIRONMENT"
    print_status $GREEN "URL: $BASE_URL"
    print_status $GREEN "Status: All checks passed"
    print_status $GREEN "Health Check: $BASE_URL/health"
    print_status $GREEN "Dashboard: $BASE_URL/Stock/Dashboard"
}

# Help function
show_help() {
    echo "Usage: $0 [ENVIRONMENT]"
    echo ""
    echo "ENVIRONMENT:"
    echo "  dev   - Validate development environment (default)"
    echo "  prod  - Validate production environment"
    echo ""
    echo "Examples:"
    echo "  $0 dev    # Validate development environment"
    echo "  $0 prod   # Validate production environment"
    echo ""
    echo "This script validates that the AI Stock Tracker application"
    echo "is deployed correctly and all endpoints are responding."
}

# Check for help flag
if [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
    show_help
    exit 0
fi

# Validate environment parameter
if [ "$ENVIRONMENT" != "dev" ] && [ "$ENVIRONMENT" != "prod" ]; then
    print_status $RED "Invalid environment: $ENVIRONMENT"
    print_status $YELLOW "Valid environments: dev, prod"
    exit 1
fi

# Run main function
main