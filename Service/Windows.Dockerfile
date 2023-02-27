# Start from Microsoft's Windows server image
FROM mcr.microsoft.com/windows/server:ltsc2022
SHELL [ "powershell" ]

# Install the .NET Core Runtime
RUN Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'C:\dotnet-install.ps1'
RUN "C:\\dotnet-install.ps1" -Channel 7.0 -Runtime dotnet -InstallDir 'C:\DotNET'
RUN setx /M PATH $($Env:PATH + ';C:\DotNET')
RUN setx /M DOTNET_ROOT 'C:\DotNET'
RUN setx /M DOTNET_CLI_TELEMETRY_OPTOUT 1

# Add artifacts from build
COPY ./ "C:\\Server-Monitor-Install"

# Move the configuration file to the system-wide configuration directory
RUN New-Item -ItemType Directory -Path 'C:\ProgramData\ServerMonitor'
RUN Move-Item -Path 'C:\Server-Monitor-Install\config.json' -Destination 'C:\ProgramData\ServerMonitor\config.json'

# Switch to the install directory
WORKDIR "C:\\Server-Monitor-Install"

# Start service when launched
ENTRYPOINT [ "dotnet", "C:\\Server-Monitor-Install\\ServerMonitor.dll" ]
