# AGENTS.md

## Project Overview

This project is a comparison between Gemini and Claude for building an AI-powered stock tracker web application. The goal is to use the provided prompt to have each AI build the application and then compare the results.

The application should have the following features:
- Search and add stocks to a watchlist by ticker symbol
- Display real-time or recent stock prices, price changes, and percentage changes
- Show a portfolio view with multiple stocks and remove functionality
- AI-powered analysis, insights, and buy/hold/sell recommendations
- Generate AI summaries of stock performance over different time periods
- Smart alerts or notifications based on AI analysis
- Clean, professional dashboard with responsive design
- Color-coded indicators and stock cards/tiles layout

## Prompt for AI Implementation

Use this exact prompt when testing with both Gemini and Claude:

```
Build an AI-powered stock tracker web application with the following features:

1. **Core functionality:**
   - Search and add stocks to a watchlist by ticker symbol
   - Display real-time or recent stock prices, price changes, and percentage changes
   - Show a portfolio view with multiple stocks
   - Remove stocks from the watchlist

2. **AI-powered features:**
   - Generate AI analysis/insights for each stock (trend analysis, basic sentiment)
   - Provide AI-generated buy/hold/sell recommendations with reasoning
   - Create AI summaries of stock performance over different time periods
   - Smart alerts or notifications based on AI analysis

3. **UI requirements:**
   - Clean, professional dashboard layout
   - Stock cards/tiles showing key metrics
   - Color-coded indicators (green/red for gains/losses)
   - Responsive design that works on mobile and desktop
   - Search bar for adding new stocks

4. **Technical specifications:**
   - Use HTML, CSS, and JavaScript (you can use a framework if preferred)
   - Integrate with a stock API (Alpha Vantage, Yahoo Finance, or similar free API)
   - Implement AI features using an AI API (OpenAI, Claude, Gemini, or mock AI responses)
   - Include error handling for API failures
   - Persist user's watchlist locally

5. **Bonus features:**
   - Historical price charts or graphs
   - Portfolio value calculation
   - News feed integration for stocks
   - Export watchlist functionality

Please create a complete, working application. Include clear instructions for any API keys needed and provide fallback/demo data if APIs aren't available. Make sure the AI features feel integrated and valuable to the user experience.
```

## Building and Running

The project will be built using HTML, CSS, and JavaScript, potentially with a framework. The specific commands for building, running, and testing the project will be determined by the AI that generates the code.

**TODO:** Add the specific commands for building, running, and testing the project once the code is generated.

## API Requirements

**TODO:** Document the required API keys and setup instructions once determined by the AI implementation:
- Stock data API (Alpha Vantage, Yahoo Finance, etc.)
- AI service API (OpenAI, Claude, Gemini, etc.)
- Any fallback/demo data mechanisms

## Development Conventions

The development conventions will be determined by the AI that generates the code.

**TODO:** Add any coding styles, testing practices, or contribution guidelines that are inferred from the generated codebase.

## Comparison Criteria

When evaluating the results from both AIs, consider:
- Code quality and organization
- UI/UX design and responsiveness
- API integration implementation
- AI feature creativity and usefulness
- Error handling and edge cases
- Documentation and setup clarity
- Performance and optimization
- Completeness of requested features