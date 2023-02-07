using System;
using System.IO;

// https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ServerMonitor {

	public static class Logging {

		// Creates a logger that writes to the console
		// https://learn.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line#non-host-console-app
		public static ILogger CreateLogger( string categoryName = "Server Monitor" ) =>
			LoggerFactory.Create( builder => {
				builder.AddConsoleFormatter<CustomConsoleFormatter, SimpleConsoleFormatterOptions>( options => {
					// Enable precise timestamps
					options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss.fff zzz] ";
					options.UseUtcTimestamp = false;

					// Enable colors
					options.ColorBehavior = LoggerColorBehavior.Enabled;

					// These do nothing as I did not implement them
					options.IncludeScopes = true;
					options.SingleLine = true;
				} );

				builder.AddConsole( options => options.FormatterName = "Custom" ); // Use the custom console formatter

				// Show all log levels in debug mode, otherwise only show information and above
				builder.AddFilter( level => {
					#if DEBUG
						return level >= LogLevel.Trace;
					#else
						return level >= LogLevel.Information;
					#endif
				} );

			} ).CreateLogger( categoryName );

	}

	// Custom implementation of the console formatter, mostly copied from the official simple console formatter
	// https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter#implement-a-custom-formatter
	// https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs
	public sealed class CustomConsoleFormatter : ConsoleFormatter, IDisposable {

		// Properties required by base classes
		private readonly IDisposable? optionsReloadToken;
		private SimpleConsoleFormatterOptions formatterOptions;

		// Constructor & methods required by base classes
		public CustomConsoleFormatter( IOptionsMonitor<SimpleConsoleFormatterOptions> options ) : base( "Custom" ) {
			optionsReloadToken = options.OnChange( ReloadLoggerOptions );
			formatterOptions = options.CurrentValue;
		}
		private void ReloadLoggerOptions( SimpleConsoleFormatterOptions options ) => formatterOptions = options;
		public void Dispose() => optionsReloadToken?.Dispose();

		// Writes the log entry
		// Example: [2022-02-06 14:22:25.000 +00:00] [INFO] (Program) Hello World
		public override void Write<TState>( in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter ) {

			// Get the message
			string? message = logEntry.Formatter?.Invoke( logEntry.State, logEntry.Exception );
			if ( message == null ) return;

			// Write our custom parts
			WriteColor( textWriter, logEntry.LogLevel );
			WriteTimestamp( textWriter );
			WriteLogLevel( textWriter, logEntry.LogLevel );

			// Write the category name (the file name in our case)
			textWriter.Write( $"({ logEntry.Category }) " );

			// Write the message
			textWriter.WriteLine( message );

			// Reset color
			if ( formatterOptions.ColorBehavior != LoggerColorBehavior.Disabled ) textWriter.Write( "\x1B[0m" );

			// End the program if it was an error or critical
			//if ( logEntry.LogLevel == LogLevel.Error || logEntry.LogLevel == LogLevel.Critical ) Environment.Exit( 1 );

		}

		// Writes the ANSI escape code to change foreground colors (handles the .ColorBehavior option)
		private void WriteColor( TextWriter textWriter, LogLevel logLevel ) {
			if ( formatterOptions.ColorBehavior == LoggerColorBehavior.Disabled ) return;

			// https://talyian.github.io/ansicolors/
			// https://stackoverflow.com/a/33206814
			string ansiColorCode = logLevel switch {
				LogLevel.Trace => "\x1B[36;3m", // Italic & Cyan
				LogLevel.Debug => "\x1B[36;22m", // Cyan
				LogLevel.Information => "\x1B[37;22m", // White
				LogLevel.Warning => "\x1B[33;22m", // Yellow
				LogLevel.Error => "\x1B[31;22m", // Red,
				LogLevel.Critical => "\x1B[31;1m", // Bold & Red,
				_ => throw new Exception( "Unrecognised log level" )
			};

			textWriter.Write( ansiColorCode );
		}

		// Writes the human-readable log level
		private void WriteLogLevel( TextWriter textWriter, LogLevel logLevel ) {
			string logLevelString = logLevel switch {
				LogLevel.Trace => "TRACE",
				LogLevel.Debug => "DEBUG",
				LogLevel.Information => "INFO",
				LogLevel.Warning => "WARN",
				LogLevel.Error => "ERROR",
				LogLevel.Critical => "CRITICAL",
				_ => throw new Exception( "Unrecognised log level" )
			};

			textWriter.Write( $"[{ logLevelString }] " );
		}

		// Writes the human-readable timestamp (handles .TimestampFormat & .UseUtcTimestamp options)
		private void WriteTimestamp( TextWriter textWriter ) {
			if ( formatterOptions.TimestampFormat == null ) return;

			DateTimeOffset currentDateTime = formatterOptions.UseUtcTimestamp == true ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
			textWriter.Write( currentDateTime.ToString( formatterOptions.TimestampFormat ) );
		}

	}

}
