## ⛔Never push sensitive information such as client id's, secrets or keys into repositories including in the README file⛔

# Employer Feedback Jobs

<img src="https://avatars.githubusercontent.com/u/9841374?s=200&v=4" align="right" alt="UK Government logo">

[![Build Status](https://sfa-gov-uk.visualstudio.com/Digital%20Apprenticeship%20Service/_apis/build/status/das-employer-feedback-jobs?repoName=SkillsFundingAgency%2Fdas-employer-feedback-jobs&branchName=main)](https://dev.azure.com/sfa-gov-uk/Digital%20Apprenticeship%20Service/_build/results?buildId=993449&view=results)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=SkillsFundingAgency_das-employer-feedback-jobs&metric=alert_status)](https://sonarcloud.io/project/overview?id=SkillsFundingAgency_das-employer-feedback-jobs)
[![Jira Project](https://img.shields.io/badge/Jira-Project-blue)](https://skillsfundingagency.atlassian.net/browse/P2-2796)
[![Confluence Project](https://img.shields.io/badge/Confluence-Project-blue)](https://skillsfundingagency.atlassian.net/wiki/spaces/NDL/pages/3773497345/Employer+Feedback+-+QF)
[![License](https://img.shields.io/badge/license-MIT-lightgrey.svg?longCache=true&style=flat-square)](https://en.wikipedia.org/wiki/MIT_License)

This azure functions solution is part of Employer Feedback project. Here we have background jobs in the form of Azure functions that carry out periodic jobs.

## How It Works

The Employer Feedback Generate Summaries job rebuilds the summary table with the latest feedback (star ratings).

The Sync Employer Accounts job ensures that the local Employer Feedback database remains in sync with the master Employer Accounts.

## 🚀 Installation

### Pre-Requisites
* A clone of this repository

## Developer Setup
### Requirements

In order to run this solution locally you will need:
- Install [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks)
- Install [.NET Core 8.0](https://www.microsoft.com/net/download)
- Install [Azure Functions SDK](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- Install [Visual Studio 2022 (Community or more advanced)](https://visualstudio.microsoft.com/vs/community/)
- Install [SQL Server 2019 (or later) Developer Edition](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
- Install [SQL Management Studio](https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)
- Install [Azure Storage Explorer](http://storageexplorer.com/)

### Config

You can find the latest config file in [das-employer-config repository](https://github.com/SkillsFundingAgency/das-employer-config/blob/master/das-employer-feedback-jobs/SFA.DAS.EmployerFeedback.Jobs.json). 

* **Azure Table Storage Explorer** - Add the following to your Azure Table Storage Explorer.

    Row Key: SFA.DAS.EmployerFeedback.Jobs_1.0

    Partition Key: LOCAL

    Data: [data](https://github.com/SkillsFundingAgency/das-employer-config/blob/master/das-employer-feedback-jobs/SFA.DAS.EmployerFeedback.Jobs.json)

Alternatively use the [das-config-updater](https://github.com/SkillsFundingAgency/das-employer-config-updater) to load all the current configurations.

In the `SFA.DAS.EmployerFeedback.Jobs` project, if not existing already, add `local.settings.json` file (Copy to Output Directory = Copy always) with following content:
```
{
  "IsEncrypted": false,
  "Values": {
    "ConfigurationStorageConnectionString": "UseDevelopmentStorage=true",
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "",
    "ConfigNames": "SFA.DAS.EmployerFeedback.Jobs",
    "EnvironmentName": "LOCAL",
    "Version": "1.0",
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SyncEmployerAccountsTimerSchedule": "0 3 * * *",
    "GenerateFeedbackSummariesFunctionTimerSchedule": "0 0 */3 * * *",
    "AppName": "SFA.DAS.EmployerFeedback.Jobs"
  }
}
```

## 🔗 External Dependencies

* The Employer Feedback Outer API defined in [das-apim-endpoints](https://github.com/SkillsFundingAgency/das-apim-endpoints/tree/master/src/EmployerFeedback) to connect to the Inner API.
* The Employer Feedback Inner API defined in [das-employer-feedback-api](https://github.com/SkillsFundingAgency/das-employer-feedback-api) to connect to the database.
* The database defined in [das-employer-feedback-api](https://github.com/SkillsFundingAgency/das-employer-feedback-api) as the primary data source.


### 📦 Internal Package Dependencies
* SFA.DAS.Configuration.AzureTableStorage

## Technologies
* .Net 8.0
* Azure Functions V4
* Azure Table Storage
* NUnit
* Moq
* FluentAssertions

