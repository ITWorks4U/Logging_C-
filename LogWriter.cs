using Logging.misc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Logging {
	/// <summary>
	/// Writer class for logging.
	/// </summary>
	public class LogWriter : IDisposable {
		#region core
		private readonly String fullLogPath;
		private String logFileName;
		private LogKind kindForLogging = LogKind.Unset;
		private readonly LogState minLogLevel;
		private readonly long maxFileSize = 0;
		private readonly int keepNumberOfFiles = 0;
		private List<String> listOfRotatingLogFiles;    // only for DailyRotation setting

		// for StreamWriter
		private StreamWriter writer;
		private DateTime currentLogDate;
		private long currentFileSize;
		private int flushCounter = 0;
		private const int flushInterval = 100;

		// for threading
		private readonly BlockingCollection<String> queue;
		private readonly CancellationTokenSource cts;
		private readonly Task worker;
		private readonly Object writerLock = new Object();
		#endregion

		/// <summary>
		/// Create a new instance for the log writer class. By default the log file is going to write to %localappdata%/current_application_name/logs.<br></br>
		/// If the folder does not exist, the folder path is going to create first. This may also throw an exception on any error.<br></br>
		/// </summary>
		/// <param name="logName">name of the log file; defaults to "app.log", if no name is given</param>
		/// <param name="minState">initial state for logging; defaults to "INFO"</param>
		/// <param name="kind">what kind of logging is in use; choseable: "Normal", "DailyRotation", "SizeRotation"</param>
		/// <param name="maxSizeMB">maximum size for a log file; only in use for "SizeRotation"</param>
		/// <param name="keepNbrOfFiles">number of files to keep; only in use for "DailyRotation" or "SizeRotation"</param>
		/// <exception cref="NotImplementedException">In case of an implementation, which is not currently in use yet.</exception>
		public LogWriter(String logName = "app.log", LogState minState = LogState.INFO, LogKind kind = LogKind.Normal, long maxSizeMB = 0, int keepNbrOfFiles = 0) {
			fullLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Process.GetCurrentProcess().ProcessName, "logs");
			Directory.CreateDirectory(fullLogPath);

			String tempLogName = logName;
			if (!tempLogName.EndsWith(".log")) {                             // if the log name doesn't end with .log, append .log
				tempLogName += ".log";
			}

			UpdateFullLogPath(tempLogName);
			minLogLevel = minState;

			switch (kind) {
				case LogKind.Normal:
					kindForLogging = LogKind.Normal;
					maxFileSize = 0;
					keepNumberOfFiles = 0;
					break;
				case LogKind.DailyRotation:                                 // does not need maxSizeMB setting
					kindForLogging = LogKind.DailyRotation;
					keepNumberOfFiles = keepNbrOfFiles;
					break;
				case LogKind.SizeRotation:
					kindForLogging = LogKind.SizeRotation;
					maxFileSize = maxSizeMB * 1024 * 1024;                  // size in MB
					keepNumberOfFiles = keepNbrOfFiles;
					break;
				default:
					String unset = $"{kindForLogging}";
					String options = $"\"{LogKind.Normal}\", \"{LogKind.DailyRotation}\", \"{LogKind.SizeRotation}\"";

					throw new ArgumentException($" Log kind is set to {unset}. Use {options} instead.");
			}

			ValidateConfiguration();                                        // check for valid log kind settings

			if (kindForLogging == LogKind.DailyRotation) {                  // only for DailyRotation
				CreateListOfLogs(initialLogName: tempLogName);
				CheckForExistingLogFiles();
			}

			writer = CreateWriter();
			currentLogDate = DateTime.Today;
			currentFileSize = File.Exists(logFileName) ? new FileInfo(logFileName).Length : 0;
			UpdateLogCreationTime();

			queue = new BlockingCollection<String>(new ConcurrentQueue<String>());
			cts = new CancellationTokenSource();

			worker = Task.Run(ProcessQueue, cts.Token);

			AddMessageToQueue(BuildStartupMessage());
		}

		/// <summary>
		/// Write a message into the log file, depending on the given log state.<br></br>
		/// If the given log state has a lower value then the initialized log state,<br></br>
		/// then no action is going to do.
		/// </summary>
		/// <param name="message">message for the log file</param>
		/// <param name="level">additional log state; defaults to TRACE logging</param>
		public void WriteToLog(String message, LogState level = LogState.TRACE) {
			if (level < minLogLevel) {
				return;
			}

			String formatted = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{level}\t{message}";
			queue.Add(formatted);

			if (level >= LogState.ERROR) {
				FlushStream();
			}
		}

		#region private methods
		#region only for DailyRotation
		/// <summary>
		/// Create a list of log files depending on the used log name. This is only for <b>DailyRotation</b> setting.<br></br>
		/// To realize this, the initial log name, like "app.log" or a custom log name is going to use, followed by<br></br>
		/// all other incremented numbers.<br></br><br></br>
		/// For instance: initialLogName = "app.log" and keepNumberOfFiles = 5, the list contains:<br></br>
		/// - app.log<br></br>
		/// - app.log.1<br></br>
		/// - app.log.2<br></br>
		/// - app.log.3<br></br>
		/// - app.log.4<br></br><br></br><br></br>
		/// 
		/// The idea is to use only those log files for a daily rotation.
		/// </summary>
		/// <param name="initialLogName">the initial log name</param>
		private void CreateListOfLogs(String initialLogName) {
			if (listOfRotatingLogFiles == null) {
				listOfRotatingLogFiles = new List<String>();
			}

			listOfRotatingLogFiles.Add(initialLogName);

			for (int i = 1; i < keepNumberOfFiles; i++) {
				listOfRotatingLogFiles.Add($"{initialLogName}.{i}");
			}
		}

		/// <summary>
		/// Check, if log files with the given log name already exists. This is only for <b>DailyRotation</b> setting.<br></br>
		/// One of three possible cases exists:<br></br>
		/// case 1:<br></br>
		/// - no log files with the key name exists yet<br></br>
		///    - use first file<br></br>
		/// case 2:<br></br>
		/// - n log files with the similar name already exists, where n > kNF<br></br>
		///    - remove all files > kNF<br></br>
		///    - rotate log files<br></br>
		///    - begin with first file name<br></br>
		/// case 3:<br></br>
		/// - n log files with the similar name already exists, where n is up to keepNumberFiles<br></br>
		///    - if n = kNF, and the creation time is equal to the current time stamp, then start with the first file name, otherwise rotate and use the first file<br></br>
		/// </summary>
		private void CheckForExistingLogFiles() {
			String lastKnownLogFileName = logFileName.Split('\\').Last();
			String[] allExistingFiles = Directory.GetFiles(fullLogPath, lastKnownLogFileName + "*");

			if (allExistingFiles.Length > 0) {                       // cases 2 or 3
				int numbersExitstingFiles = allExistingFiles.Length;

				if (keepNumberOfFiles > numbersExitstingFiles) {     // case 3 only
					/* case 3 only:
					 * before to start to rotate, check, if the last detected
					 * file's creation time is today; if true, then use this
					 * file instead and don't rotate
					*/
					String lastFile = allExistingFiles.Last();
					DateTime dt = new FileInfo(lastFile).CreationTime;
					if (dt == DateTime.Today) {
						UpdateFullLogPath(lastFile);
						return;
					}

					RotationSequence(startPosition: numbersExitstingFiles);

					// now re-create the first log file name, where
					// the creation time is set to today (required)
					File.Create(logFileName).Close();
					_ = new FileInfo(logFileName).CreationTime = DateTime.Today;
				} else {                                             // case 2 only
					List<String> reversedList = allExistingFiles.Reverse().ToList();

					foreach (String s in reversedList) {
						if (!listOfRotatingLogFiles.Contains(s.Split('\\').Last())) {
							File.Delete(s);
						}
					}

					RotationSequence(startPosition: listOfRotatingLogFiles.Count - 1);
				}
			}                                                        // otherwise this is case 1 (no need to do anything here)
		}
		#endregion

		/// <summary>
		/// Update the full log path with the new log name.
		/// </summary>
		/// <param name="logName">the new log name for the full log path</param>
		private void UpdateFullLogPath(String logName) {
			logFileName = Path.Combine(fullLogPath, logName);
		}

		/// <summary>
		/// Validate the log configurations.<br></br>
		/// SizeRotation:<br></br>
		/// - maxFileSize > 0<br></br><br></br>
		/// DailyRotation / SizeRotation:<br></br>
		/// - keepNumberOfFiles > 0<br></br><br></br>
		/// 
		/// If one of those settings is wrongly set, then the log becomes
		/// a normal log without any rotation.
		/// </summary>
		private void ValidateConfiguration() {
			if (kindForLogging == LogKind.SizeRotation) {
				if (maxFileSize <= 0) {
					kindForLogging = LogKind.Normal;
					return;
				}
			}

			if (kindForLogging != LogKind.Normal && keepNumberOfFiles <= 0) {
				kindForLogging = LogKind.Normal;
			}
		}

		/// <summary>
		/// Create and return the streamwriter object for the whole logging process.<br></br>
		/// The streamwriter allows to append new text and the encoding is set to UTF-8.<br></br>
		/// Finally, no automatic stream flush is active.
		/// </summary>
		private StreamWriter CreateWriter() {
			return new StreamWriter(logFileName, append: true, Encoding.UTF8) {
				AutoFlush = false
			};
		}

		/// <summary>
		/// Launch a new task for the full logging procedure.
		/// </summary>
		private async Task ProcessQueue() {
			//NOTE: When the StreamWriter instance has been closed or disposed,
			//      and the queue contains n entries (n > 0), then an OperationCanceledException
			//      is going to throw. In that case a log file may not contain the full
			//      event data.
			//
			//      This can be "fixed" in the Dispose() method, if the writer object
			//      has not been touched, however, this may cause an IOException
			//      after terminating this LogWriter class, when an another process
			//      still tries to access to the current log file.
			try {
				foreach (String message in queue.GetConsumingEnumerable(cts.Token)) {
					lock (writerLock) {
						if (CheckForRotation()) {
							RotateFiles();
						}

						writer?.WriteLine(message);
						currentFileSize += Encoding.UTF8.GetByteCount(message + Environment.NewLine);

						flushCounter++;

						if (flushCounter >= flushInterval) {
							FlushStream();
							flushCounter = 0;
						}
					}
				}
			} catch (OperationCanceledException oce) {
				Debug.WriteLine($"Logger canceled: {oce}");
			} catch (Exception ex) {
				Debug.WriteLine($"Logger crashed: {ex}");
			} finally {
				lock (writerLock) {
					FlushStream();
					writer?.Dispose();
				}
			}
		}

		/// <summary>
		/// Check, if a file needs a rotation. Only works for DailyRotation or SizeRotation.
		/// </summary>
		/// <returns>true, if a rotation is going to do; otherwise false</returns>
		private bool CheckForRotation() {
			switch (kindForLogging) {
				case LogKind.DailyRotation:
					return new FileInfo(logFileName).CreationTime != currentLogDate;
				case LogKind.SizeRotation:
					return currentFileSize >= maxFileSize;
				default:   // normal only
					return false;
			}
		}

		/// <summary>
		/// Do a file rotation in sequence. The upper boundary setting<br></br>
		/// determines from which start position the rotation begins.
		/// </summary>
		/// <param name="startPosition">start position for rotation</param>
		private void RotationSequence(int startPosition) {
			for (int i = startPosition; i > 0; i--) {
				String src = (i - 1) == 0 ? logFileName : $"{logFileName}.{i - 1}";
				String dst = $"{logFileName}.{i}";

				if (File.Exists(dst)) {
					File.Delete(dst);
				}

				if (File.Exists(src)) {
					File.Move(src, dst);
				}
			}
		}

		/// <summary>
		/// Preset for a file rotation. Before a rotation starts,<br></br>
		/// the writer needs to be disposed and recreated after rotation.
		/// </summary>
		private void RotateFiles() {
			FlushStream();
			writer?.Dispose();

			RotationSequence(startPosition: keepNumberOfFiles - 1);

			writer = CreateWriter();
			currentLogDate = DateTime.Today;
			currentFileSize = 0;
			UpdateLogCreationTime();
		}

		/// <summary>
		/// Modify the creation time of the current log date.
		/// This is more required for DailyRotation, otherwise
		/// a log rotation may be done, even this shouldn't.
		/// </summary>
		private void UpdateLogCreationTime() {
			_ = new FileInfo(logFileName).CreationTime = currentLogDate;
		}

		/// <summary>
		/// Flush the current file stream.
		/// </summary>
		private void FlushStream() {
			lock (writerLock) {
				writer?.Flush();
			}
		}

		/// <summary>
		/// message for log file for the next new session
		/// </summary>
		/// <returns>a header for a new log session</returns>
		private String BuildStartupMessage() {
			String separator = "==============================";
			String header = String.Format(
				"{1}{0}" +
				"{2}{0}" +
				"{1}",
				Environment.NewLine,
				separator,
				"New log session started."
			);

			return header;
		}

		/// <summary>
		/// Add the next message into the message queue.
		/// </summary>
		/// <param name="message">the message to add</param>
		private void AddMessageToQueue(String message) {
			queue.Add(message);
		}
		#endregion

		/// <summary>
		/// Clean up.
		/// </summary>
		public void Dispose() {
			queue.CompleteAdding();
			cts.Cancel();

			try {
				worker.Wait();
			} catch (Exception e) {
				Debug.WriteLine(e.ToString());
			}

			queue.Dispose();
			cts.Dispose();

			// NOTE: perhaps this causes the OperationCanceledException
			//       in the ProcessQueue method
			writer?.Close();
			writer?.Dispose();
		}
	}
}