using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace Rebex.TinySftpServer
{
	public class Config
	{
		/// <summary>
		/// Product homepage URL
		/// </summary>
		/// <remarks>
		/// For Google Analytics params
		/// see https://support.google.com/analytics/answer/1033867?hl=en
		/// </remarks>
		public const string HomepageUrl = "https://www.rebex.net/tiny-sftp-server/?utm_source=TinySftpServerApp&utm_medium=application&utm_content=MainScreenLink&utm_campaign=TinySftpServerAppLinks";

		/// <summary>
		/// Buru Server URL
		/// </summary>
		public const string BuruServerUrl = "https://www.rebex.net/buru-sftp-server/?utm_source=TinySftpServer";

		/// <summary>
		/// SFTP user name
		/// </summary>
		public string UserName { get { return GetValue("userName", "tester"); } }

		/// <summary>
		/// Password for SFTP user
		/// </summary>
		public string UserPassword { get { return GetValue("userPassword", "password"); } }

		/// <summary>
		/// Directory for storing user public key file(s) (optional)
		/// </summary>
		public string UserPublicKeyDir
		{
			get
			{
				var path = GetValue("userPublicKeyDir", string.Empty);
				if (string.IsNullOrWhiteSpace(path))
					return string.Empty;

				try
				{
					return NormalizeWritableDirPath(path);
				}
				catch (Exception ex)
				{
					throw new ConfigurationErrorsException("Invalid path. Check 'userPublicKeyDir' value in config file.", ex);
				}
			}
		}

		/// <summary>
		/// User root dir
		/// </summary>
		public string UserRootDir
		{
			get
			{
				var path = GetValue("userRootDir", "");
				if (string.IsNullOrWhiteSpace(path))
					throw new ConfigurationErrorsException("No data path specified. Set value 'userRootDir' in config file.");

				try
				{
					return NormalizeWritableDirPath(path);
				}
				catch (Exception ex)
				{
					throw new ConfigurationErrorsException("Invalid path. Check 'userRootDir' value in config file.", ex);
				}
			}
		}

		/// <summary>
		/// Password for RSA private key. 
		/// </summary>
		public string RsaPrivateKeyPassword { get { return GetValue("rsaPrivateKeyPassword", ""); } }

		/// <summary>
		/// Path to RSA private key file.
		/// </summary>
		public string RsaPrivateKeyFile
		{
			get
			{
				return NormalizeWritableFilePath(GetValue("rsaPrivateKeyFile", "server-private-key-rsa.ppk"));
			}
		}

		/// <summary>
		/// Password for DSS private key. 
		/// </summary>
		public string DssPrivateKeyPassword { get { return GetValue("dssPrivateKeyPassword", ""); } }
		/// <summary>
		/// Path to DSS private key file.
		/// </summary>
		public string DssPrivateKeyFile
		{
			get
			{
				return NormalizeWritableFilePath(GetValue("dssPrivateKeyFile", "server-private-key-dss.ppk"));
			}
		}

		/// <summary>
		/// SFTP server port. If not specified port 22 is used.
		/// </summary>
		public int ServerPort
		{
			get
			{
				var value = GetValue("sshPort", "22");
				int result;
				if (!Int32.TryParse(value, out result))
				{
					throw new ConfigurationErrorsException("Invalid value for 'sshPort' in config file.");
				}
				return result;
			}
		}

		/// <summary>
		/// True if server should be started when app starts.
		/// </summary>
		public bool AutoStart
		{
			get
			{
				var value = GetValue("autoStart", "false");
				return Boolean.Parse(value);
			}
		}

		public bool ShowUserDetails
		{
			get
			{
				var value = GetValue("showUserDetailsOnStartup", "true");
				return Boolean.Parse(value);
			}
		}

		/// <summary>
		/// Initializes new instance of <see cref="Config"/> and verifies that the config file is readable.
		/// </summary>
		public Config()
		{
			// Ensure that configuration file is readable.
			Verify();
		}

		public void Verify()
		{
			try
			{
				ConfigurationManager.AppSettings.GetEnumerator().Reset();
			}
			catch (ConfigurationException ex)
			{
				var errors = ex.InnerException as ConfigurationErrorsException;
				Exception detail = null;
				if (errors != null)
				{
					foreach (var error in errors.Errors)
					{
						detail = error as Exception;
						if (detail != null) break;
					}
				}

				string message = "Unable to load configuration file.";
				if (detail != null)
				{
					message += " " + detail.Message;
				}

				throw new ConfigurationErrorsException(message, ex);
			}
		}

		private string GetValue(string key, string defaultValue)
		{
			var result = ConfigurationManager.AppSettings[key];
			if (string.IsNullOrEmpty(result))
				return defaultValue;

			return result;
		}

		public string GetAlternativeDataDir()
		{
			var rootPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			var dir = Path.Combine(rootPath, "RebexTinySftpServer");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			return dir;
		}

		public IEnumerable<IPAddress> GetAllLocalIPAddresses() {
			string hostName = Dns.GetHostName();

			IPHostEntry ipHostEntry = Dns.GetHostEntry(hostName);

			return ipHostEntry.AddressList;
		}

		public string ConfigurationFilePath
		{
			get
			{
				return AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
			}
		}

		public string ProductVersion
		{
			get
			{
				Assembly assembly = Assembly.GetExecutingAssembly();
				FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

				var result = string.Format(
					"{0} v{1}.{2}.{3}",
					fvi.ProductName,
					fvi.ProductMajorPart,
					fvi.ProductMinorPart,
					fvi.ProductBuildPart
					);

				return result;
			}
		}

		/// <summary>
		/// Gets the normalized full path of the requested path.
		/// If the path is relative, ensures that a file can be created there.
		/// </summary>
		private string NormalizeWritableFilePath(string path)
		{
			// Just normalize the path if rooted.
			if (Path.IsPathRooted(path))
				return Path.GetFullPath(path);

			try
			{
				// Path is relative to the executable directory.
				string baseDir = Path.GetDirectoryName(Application.ExecutablePath);
				string dir = Path.GetFullPath(Path.Combine(baseDir, path));

				if (File.Exists(dir))
					return dir;

				// Can it be written?
				File.WriteAllText(dir, "dummy");
				File.Delete(dir);
				return dir;
			}
			catch (UnauthorizedAccessException)
			{
				// Return alternative path
				string baseDir = GetAlternativeDataDir();
				return Path.GetFullPath(Path.Combine(baseDir, path));
			}
		}

		/// <summary>
		/// Gets the normalized full path of the requested path.
		/// If the path is relative, ensures that a directory can be created there.
		/// </summary>
		private string NormalizeWritableDirPath(string path)
		{
			// Just normalize the path if rooted.
			if (Path.IsPathRooted(path))
				return Path.GetFullPath(path);

			try
			{
				// Path is relative to the executable directory.
				string baseDir = Path.GetDirectoryName(Application.ExecutablePath);
				string dir = Path.GetFullPath(Path.Combine(baseDir, path));

				if (File.Exists(dir))
					return dir;

				// Can it be written?
				Directory.CreateDirectory(dir);
				return dir;
			}
			catch (UnauthorizedAccessException)
			{
				// Return alternative path
				string baseDir = GetAlternativeDataDir();
				return Path.GetFullPath(Path.Combine(baseDir, path));
			}
		}
	}
}
