using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Logging {
	/// <summary>
	///	Scanning the given folder for outdated and emtpy logs.<br></br>
	///	Any occurred exception is NOT going to handle here.
	/// </summary>
	[Serializable]
	public class LogScanner : IDisposable {
		private readonly String logFileDestination;

		#region properties
		/// <summary>
		/// List of all gotten log files.
		/// </summary>
		public List<String> ListOfAllLogFiles { get; private set; }

		/// <summary>
		/// List of all log files, which are going to remove.
		/// </summary>
		public List<String> ListOfDisposableLogs { get; private set; } = new List<String>();
		#endregion

		#region constructor
		/// <summary>
		/// Creating a new instance of the scanner.
		/// </summary>
		/// <param name="logFileDestination">path to look for log files</param>
		/// <exception cref="IOException">If the given log file destination was not found.</exception>
		public LogScanner(String logFileDestination) {

			if (String.IsNullOrEmpty(logFileDestination)) {
				throw new ArgumentNullException(nameof(logFileDestination));
			}

			if(!Directory.Exists(logFileDestination)) {
				throw new IOException(message: $"The expected path \"{logFileDestination}\" was not found.");
			}

			this.logFileDestination = logFileDestination;
		}
		#endregion

		#region methods
		/// <summary>
		/// Scan for old logs. The default maximum amount of days to keep logs is set to 90 days.<br></br>
		/// Each log file, which creation time lies beyond the maximum amount of days to keep will be marked<br></br>
		/// for deleting. This also appears for each empty log file.<br></br>
		/// If the argument deleteNow is true, then all marked log files to delete are going to delete.<br></br><br></br>
		/// 
		/// In case of the maxAmountOfDays argument contains a negative number an ArgumentException is going to throw.
		/// </summary>
		/// <param name="maxAmountOfDays">the amount of days for logs to keep</param>
		/// <param name="deleteNow">remove each collected old or empty log file</param>
		/// <exception cref="ArgumentException">in case of the maxAmountOfDays argument contains a negative number</exception>
		public void ScanLogs(int maxAmountOfDays = 90,bool deleteNow = false) {
			if(maxAmountOfDays < 0) {
				throw new ArgumentException($"Argument \"max amount of days\" ({maxAmountOfDays}) must not be negative.");
			}

			ListOfAllLogFiles = Directory.GetFiles(logFileDestination, "*.log").ToList();

			foreach (String logFile in ListOfAllLogFiles) {
				FileInfo fi = new FileInfo(logFile);

				//	empty log file
				if(fi.Length == 0) {
					ListOfDisposableLogs.Add(fi.FullName);
					continue;
				}

				//	check for outdated log files
				TimeSpan ts = DateTime.Now - fi.CreationTime;

				if(ts.Days > maxAmountOfDays) {
					ListOfDisposableLogs.Add(fi.FullName);
				}
			}

			if(deleteNow) {
				ClearLogs();
			}
		}

		/// <summary>
		/// Removing old or empty logs.
		/// </summary>
		public void ClearLogs() {
			int position = 0;

			while (position != ListOfDisposableLogs.Count) {
				String oldLog = ListOfDisposableLogs.ElementAt(position);
				File.Delete(oldLog);
				position++;
			}

			foreach (String logFile in ListOfAllLogFiles) {
				if (!File.Exists(logFile)) {
					ListOfDisposableLogs.Remove(logFile);
				}
			}
		}

		/// <summary>
		/// Clean up.
		/// </summary>
		public void Dispose() { }
		#endregion
	}
}
