# Privacy Policy

**Last Update: 7th April 2023 at 12:00 BST.**

## What data is collected?

This project (i.e., the front-end Android application & the back-end .NET service) does not collect any user data. This includes personally identifable information, usage analytics, advertising, statistics, etc.

The only data collected is system metrics via a Prometheus exporter (e.g., resource utilisation, hardware sensor readings, service & Docker container statuses, etc.) for displaying on the front-end Android application. This is required for functionality as the goal of this project is to create "a mobile app for monitoring servers".

## How is the data used?

This project cannot analyse or sell user data as none is collected.

The system metrics are used to display status information on the front-end Android application.

## How is the data stored?

This project cannot store user data as none is collected.

The system metrics are stored in a Prometheus time-series database for a maximum of 7 days (by default).

Due to this project's self-hosted nature, all data involved in using the project is stored locally on your own system.
