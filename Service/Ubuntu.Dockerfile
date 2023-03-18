# Start from my .NET Core Runtime
FROM ghcr.io/viral32111/dotnet:7.0-ubuntu

# Configure directories & files
ARG SERVERMONITOR_DIRECTORY=/usr/local/server-monitor \
	SERVERMONITOR_CONFIG_DIRECTORY=/etc/server-monitor

# Install required packages
RUN apt-get update && \
	apt-get install --no-install-recommends --yes systemctl dbus && \
	apt-get clean --yes && \
	rm --verbose --recursive /var/lib/apt/lists/*

# Add artifacts from build
COPY --chown=${USER_ID}:${USER_ID} ./ ${SERVERMONITOR_DIRECTORY}

# Move the configuration file to the system-wide configuration directory
RUN mkdir --verbose --parents ${SERVERMONITOR_CONFIG_DIRECTORY} && \
	mv --verbose ${SERVERMONITOR_DIRECTORY}/config.json ${SERVERMONITOR_CONFIG_DIRECTORY}/config.json && \
	chown --changes --recursive ${USER_ID}:${USER_ID} ${SERVERMONITOR_CONFIG_DIRECTORY}

# Switch to the regular user, in the install directory
USER ${USER_ID}:${USER_ID}
WORKDIR ${SERVERMONITOR_DIRECTORY}

# Start service when launched
ENTRYPOINT [ "dotnet", "/usr/local/server-monitor/ServerMonitor.dll" ]
