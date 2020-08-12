using System.Drawing;

namespace Rebex.TinySftpServer
{
	public static class LogColor
	{
		// colors
		internal static readonly Color Response  = Color.Black;      // response color
		internal static readonly Color Command   = Color.DarkGreen;  // command color
		internal static readonly Color Error     = Color.Red;        // color of error messages
		internal static readonly Color Info      = Color.Blue;       // info color
		internal static readonly Color Ssh       = Color.BlueViolet; // color of SSH communication
		internal static readonly Color Tls       = Color.BlueViolet; // color of TLS communication
		internal static readonly Color Important = Color.DarkRed;    // important messages
		internal static readonly Color Success   = Color.DarkGreen;  // success color
	}
}
