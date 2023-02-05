# Start from the .NET Core Runtime
FROM ghcr.io/viral32111/dotnet:7.0

# Configure directories & files
ARG SERVERMONITOR_DIRECTORY=/usr/local/server-monitor

# Add artifacts from build
COPY --chown=${USER_ID}:${USER_ID} ./ ${SERVERMONITOR_DIRECTORY}

# Switch to the regular user, in the install directory
USER ${USER_ID}:${USER_ID}
WORKDIR ${SERVERMONITOR_DIRECTORY}

# Start the bot when launched
ENTRYPOINT [ "dotnet", "/usr/local/server-monitor/ServerMonitor.dll" ]
