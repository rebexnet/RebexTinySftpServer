using System;
using System.Windows.Forms;

namespace Rebex.TinySftpServer
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			// purchase a licsense for https://www.rebex.net/file-server/
			// or get your 30-day trial key at https://www.rebex.net/support/trial/
			Rebex.Licensing.Key = LicenseKey.Value;

			// display an error dialog when an unhandled error occurs
			AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
			{
				var ex = e.ExceptionObject as Exception;
				string message = (ex != null) ? ex.Message : string.Concat(e.ExceptionObject);
				MessageBox.Show("Error: " + message, "Rebex Tiny SFTP Server", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
			};

			// enable styles and run the application
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
