# Logging module (C#)

-   C# .NET Framework 4.8
    -   also runs with .NET 10
-   Windows System (requires at least support for .NET Framework 4.8)

| details |
| - |
| author: itworks4u |
| created: March 18th, 2026 |
| updated: March 21st, 2026 |
| version: 1.2.1 |

##  description
-   comes with LogScanner and LogWriter class

| class | description |
| - | - |
| LogScanner | Scanning for log files and remove old or empty logs. |
| LogWriter | Write a new log file, where certain options can be set. |

### LogScanner
-   scans log files from a given destination folder
    -   if the destination path is `null` or `empty`, an `ArgumentNullException` is going to throw
    -   if the destination folder wasn't found, an `IOException` is going to throw
-   offers to mark log files, which maximum amount of days has been exceeded
    -   optional: allows to remove those log files immediately
-   can be used with `using` directive

```
public LogScanner(String logFileDestination) {}

/////
/////
/////

using (LogScanner ls = new LogScanner("path_to_use")) {
    // some actions to do
}

```

### LogWriter
-   offers to write a log file
    -   default log path refers to `%localappdata%/current_application_name/logs`
    -   if the destination path doesn't exist, this will be created by default
    -   if the log name is unset, `app.log` is going to use
        -   if this file already exists, the new content will be added to the existing file
-   minimal log state refers to `NORMAL`
    -   levels: TRACE, DEBUG, NORMAL, WARNING, ERROR, FATAL
    -   every event, which has a lower level priority than the initialized log level won't be handled
-   comes with different log kinds
-   can be used with `using` directive

| kind | description | additional informations
| - | - | - |
| Normal | write everything into the given log file without any rotation | does not rotate anytime |
| DailyRotation | rotate log files, when a new day has been detected | keepNbrOfFiles `<1`: kind becomes to Normal |
| SizeRotation | rotate log files, when a certain file size has been reached | keepNbrOfFiles `<1` or maxSizeMB `<1`: kind becomes to Normal |
| Unset | only used for test cases | - |

```
public LogWriter(String logName = "app.log", LogState minState = LogState.INFO, LogKind kind = LogKind.Normal, long maxSizeMB = 0, int keepNbrOfFiles = 0) {}

public void WriteToLog(String message, LogState level = LogState.TRACE) {}

/////
/////
/////

using (LogWriter lw = new LogWriter()) {
    //  some actions to do
}
```