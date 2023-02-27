# Start from Microsoft's .NET Core Runtime
FROM mcr.microsoft.com/dotnet/runtime:7.0-windowsservercore-ltsc2022
SHELL [ "powershell" ]

# Add artifacts from build
COPY ./ C:\\Server-Monitor-Install

# Move the configuration file to the system-wide configuration directory
RUN New-Item -ItemType Directory -Path C:\\ProgramData\\ServerMonitor
RUN Move-Item -Path C:\\Server-Monitor-Install\\config.json -Destination C:\\ProgramData\\ServerMonitor\\config.json

# Switch to the install directory
WORKDIR C:\\Server-Monitor-Install

# Start service when launched
ENTRYPOINT [ "dotnet", "C:\\Server-Monitor-Install\\ServerMonitor.dll" ]
