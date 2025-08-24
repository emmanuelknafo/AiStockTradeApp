# infrastructure

This folder contains infrastructure-as-code for cloud deployments.

Contents

- `main.bicep` - Primary Bicep template that provisions Azure resources for the application.
- `parameters.dev.json` / `parameters.prod.json` - Environment-specific parameter files.

Notes

- Use the Azure Developer CLI (`azd`) or `az deployment` to deploy this Bicep template. Review parameter files before deploying.
