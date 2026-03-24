namespace Logging.misc {
	/// <summary>
	/// Different kind of log states.
	/// </summary>
	public enum LogState {
		/// <summary>
		/// Lowest log type. Contains everything.
		/// </summary>
		TRACE,

		/// <summary>
		/// Contains at least debug messages or higher.
		/// </summary>
		DEBUG,

		/// <summary>
		/// Offers to write information messages or higher.
		/// </summary>
		INFO,

		/// <summary>
		/// Warning messages or higher.
		/// </summary>
		WARNING,

		/// <summary>
		/// Error messages or higher.
		/// </summary>
		ERROR,

		/// <summary>
		/// The highest possible setting. Each message with a lower state<br></br>
		/// won't be handled for logging.
		/// </summary>
		FATAL
	}
}