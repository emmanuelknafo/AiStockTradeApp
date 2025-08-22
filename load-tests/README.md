AiStockTradeApp — Load tests (JMeter)

Purpose

This folder contains a separate load-test project using Apache JMeter. It includes a minimal JMeter test plan, a PowerShell runner that runs JMeter via Docker, and a small GitHub Actions workflow to run the tests.

Prerequisites

- Docker (recommended) OR a local Apache JMeter installation (5.x).
- PowerShell (Windows) for the included runner script.

Quickstart (recommended: Docker)

1. From the repository root:

```powershell
cd .\load-tests

# Run with default values (host=localhost port=5000 threads=10 ramp=10 loop=1)
.\run-jmeter.ps1

# Example: run directly with Docker and customize parameters
docker run --rm -v "${PWD}\\jmeter:/tests" -w /tests justb4/jmeter:5.5 -n -t test-plan.jmx -l results.jtl -Jhost=localhost -Jport=5000 -Jthreads=50 -Jramp=30 -Jloop=10
```

Or run JMeter locally (if installed):

```powershell
# from load-tests\jmeter
jmeter -n -t test-plan.jmx -l results.jtl -Jhost=localhost -Jport=5000 -Jthreads=50 -Jramp=30 -Jloop=10
```

Files added

- `jmeter/test-plan.jmx` — minimal test plan that sends GET requests to `/api/health` using properties `host` and `port`. Thread count, ramp and loop defaults are controlled via JMeter properties `threads`, `ramp`, and `loop`.
- `run-jmeter.ps1` — convenience runner for Docker on Windows/PowerShell (auto-maps `load-tests/jmeter`).
- `.gitignore` — ignore results files.
- `.github/workflows/load-tests.yml` — optional workflow that runs the test via Docker using `justb4/jmeter`.

Notes & next steps

- Edit `jmeter/test-plan.jmx` to add additional HTTP samplers (endpoints), CSV data sets, assertions, timers, and listeners.
- If you prefer Azure Pipelines, I can add an Azure pipeline YAML that runs these tests in a self-hosted/hosted runner.

If you want, I can also:
- Add a richer test plan with multiple endpoints and CSV parameterization.
- Configure thresholds/alerts and parse results into HTML reports.

## Azure Pipelines

A corresponding Azure DevOps pipeline YAML is included at `azure-pipelines/load-tests.yml`.

How to use

- In Azure DevOps create a new pipeline and point it to the repository file `azure-pipelines/load-tests.yml`.
- The pipeline is configured for manual runs (no CI trigger). It runs JMeter in Docker on an `ubuntu-latest` agent and publishes `results.jtl` as a pipeline artifact.

Customize run parameters by editing the variables at the top of `azure-pipelines/load-tests.yml` (host, port, threads, ramp, loop).
