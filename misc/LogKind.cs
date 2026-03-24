namespace Logging.misc {
	/// <summary>
	/// Selection of how to handle the log file. Can be separated into:<br></br><br></br>
	/// - <tt><see cref="LogKind.Normal"/>:</tt>        limit will be ignored<br></br>
	/// - <tt><see cref="LogKind.DailyRotation"/>:</tt> rotation to the next file, when a new day has been detected<br></br>
	/// - <tt><see cref="LogKind.SizeRotation"/>:</tt>  rotation to the next file, when the amount of size in <b>MB</b> has been reached
	/// </summary>
	public enum LogKind {
		/// <summary>
		/// no modification of max lines or max file size
		/// </summary>
		Normal,

		/// <summary>
		/// Rotation to the next log file, when a new day has been detected.
		/// </summary>
		DailyRotation,

		/// <summary>
		/// Rotation to the next log file, when the amount of size in <b>MB</b> has been reached.
		/// </summary>
		SizeRotation,

		/// <summary>
		/// Currently unused for anything. Perhaps useful for future implementations.
		/// </summary>
		Unset
	}
}

