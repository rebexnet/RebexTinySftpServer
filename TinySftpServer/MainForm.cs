using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Rebex.Net;
using Rebex.Net.Servers;
using Rebex.Security.Certificates;

namespace Rebex.TinySftpServer
{
	public partial class MainForm : Form
	{
		public const LogLevel DefaultLogLevel = LogLevel.Info;

		public bool IsStarted;
		public bool IsExit;
		public bool MinimizeToTray;
		public FileServer Server;
		public Config Config;
		public SshPublicKey[] UserPublicKeys;
		public RichTextBoxLogWriter Log;

		public MainForm()
		{
			InitializeComponent();
			InitFields();
			InitUI();
		}

		private void InitFields()
		{
			Config = new Config();
			Log = new RichTextBoxLogWriter(LogRichTextBox, DefaultLogLevel);
			Server = new FileServer() { LogWriter = Log };
			MinimizeToTray = Config.MinimizeToTray;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			SetupServer();
			if (Config.MinimizeOnStart)
			{
				WindowState = FormWindowState.Minimized;
			}
			if (MinimizeToTray)
			{
				NotifyIcon.Visible = true;
			}
		}

		private void InitUI()
		{
			// set icon and logo
			var assembly = GetType().Assembly;
			string resourcePrefix = "Rebex.TinySftpServer.Resources.";
			Icon = new Icon(assembly.GetManifestResourceStream(resourcePrefix + "TinySftpServer.ico"));
			pictureBox2.Image = Image.FromStream(assembly.GetManifestResourceStream(resourcePrefix + "RebexLogo.png"));
			menuItemShow.Image = Icon.ToBitmap();
			NotifyIcon.Icon = Icon;

			// text for About box
			var sb = new StringBuilder();

			sb.Append(@"{\rtf\ansi");
			sb.Append(@"\b " + Config.ProductVersion + @" \b0 \line ");
			sb.Append(@"Free minimalist SFTP server with no setup and modern cipher support. \line ");
			sb.Append(@"\line ");
			sb.Append(@"Server IP: \tab ");

			string indent = "";
			foreach (var ipAddress in Config.GetAllLocalIPAddresses().OrderBy(ip => ip.AddressFamily))
			{
				if (ipAddress.AddressFamily == AddressFamily.InterNetwork ||
					ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
				{
					sb.Append(indent + ipAddress + @" \line ");
					indent = @" \tab \tab ";
				}
			}

			sb.Append(@"\line ");
			sb.Append(@"Server port: \tab " + Config.ServerPort + @" \line ");

			if (Config.ShowUserDetails)
			{
				sb.Append(@"\line ");
				sb.Append(@"User:          \tab " + RtfEscapeString(Config.UserName) + @"\line ");
				sb.Append(@"Password:      \tab " + RtfEscapeString(Config.UserPassword) + @"\line ");

				if (string.IsNullOrEmpty(Config.UserPublicKeyDir))
					sb.Append(@"User public keys:   \tab \i (disabled) \i0 \line ");
				else
					sb.Append(@"User public keys:   \tab " + RtfEscapeString(Config.UserPublicKeyDir) + @"\line ");

				sb.Append(@"User root directory: \tab " + RtfEscapeString(Config.UserRootDir) + @"\line ");
			}

			sb.Append(@"Configuration file: \tab " + RtfEscapeString(Config.ConfigurationFilePath) + @"\line ");
			sb.Append(@"}");

			AboutBox.Rtf = sb.ToString();

			// setup log level list
			var logLevels = Enum.GetValues(typeof(LogLevel));
			LogLevelCombo.DataSource = logLevels;
			LogLevelCombo.SelectedItem = DefaultLogLevel;
		}

		public List<SshPublicKey> GetUserPublicKeys(string userPublicKeyDir)
		{
			Log.Write("");

			var list = new List<SshPublicKey>();
			string[] files;
			try
			{
				files = Directory.GetFiles(userPublicKeyDir);
			}
			catch (Exception)
			{
				Log.Write(LogColor.Important, "Unable to access user public key directory '{0}'.", userPublicKeyDir);
				return list;
			}

			for (int i = 0; i < files.Length; i++)
			{
				string file = files[i];
				try
				{
					SshPublicKey key;
					switch (Path.GetExtension(file).ToLowerInvariant())
					{
						case ".pub":
						case ".key":
							key = new SshPublicKey(file);
							Log.Write("User public key '{0}' loaded.\r\nFingerprint: {1}", file, key.Fingerprint.ToString(SignatureHashAlgorithm.MD5));
							list.Add(key);
							break;

						case ".der":
						case ".cer":
						case ".crt":
							var certificate = Certificate.LoadDer(file);
							key = new SshPublicKey(certificate);
							Log.Write("User public key '{0}' loaded.\r\nFingerprint: {1}", file, key.Fingerprint.ToString(SignatureHashAlgorithm.MD5));
							list.Add(key);
							break;

						case "":
							if (Path.GetFileName(file) != "authorized_keys")
								goto default;

							var keys = SshPublicKey.LoadPublicKeys(file);
							if (keys.Length > 0)
							{
								Log.Write("User public keys '{0}' loaded.", file);
								foreach (var item in keys)
								{
									Log.Write("Fingerprint: {0}", item.Fingerprint.ToString(SignatureHashAlgorithm.MD5));
								}
								list.AddRange(keys);
							}
							break;

						default:
							Log.Write(LogColor.Important, "User public key '{0}' file extension is unknown.", file);
							Log.Write(LogColor.Info, "Supported extensions for SSH public keys: '*.pub', '*.key'.");
							Log.Write(LogColor.Info, "Supported extensions for X509 certificates: '*.der', '*.cer', '*.crt'.");
							Log.Write(LogColor.Info, "Supported file name for ~/.ssh/authorized_keys file format: 'authorized_keys'.");
							continue;

					}
				}
				catch (Exception x)
				{
					Log.Write(LogColor.Important, "User public key '{0}' could not be loaded: {1}", file, x.Message);
				}
			}

			return list;
		}

		private void TryAddPrivateKey(string keyPath, string keyPassword, bool useRsa)
		{
			// Temporary hotfix for WinXP which is (sometimes?) unable to generate DSA keys
			try
			{
				SshPrivateKey key = LoadOrCreatePrivateKey(keyPath, keyPassword, useRsa);
				Server.Keys.Add(key);
			}
			catch (CryptographicException ex)
			{
				Log.Write(ex);
			}
		}

		public void SetupServer()
		{
			try
			{
				// 1. Server keys

				// add private keys for server authentication
				TryAddPrivateKey(Config.RsaPrivateKeyFile, Config.RsaPrivateKeyPassword, useRsa: true);
				TryAddPrivateKey(Config.DssPrivateKeyFile, Config.DssPrivateKeyPassword, useRsa: false);

				// 2. Users

				// make sure that root path does exists
				if (!Directory.Exists(Config.UserRootDir))
				{
					var dir = Config.UserRootDir;
					Log.Write(LogColor.Important, "User data root directory '{0}' does not exist.", dir);
					Log.Write("Creating data root directory...");
					Directory.CreateDirectory(dir);

					// create test files
					Log.Write("Creating user test data...");
					var testFileDataPath = Path.Combine(dir, "testfile.txt");
					File.WriteAllText(
						testFileDataPath,
						"This is a test file created by Rebex Tiny SFTP server." +
						Environment.NewLine + Environment.NewLine +
						"https://www.rebex.net/tiny-sftp-server/"
					);
					Log.Write("Done.");
				}

				// add user
				var user = new FileServerUser(
					Config.UserName,
					Config.UserPassword,
					Config.UserRootDir);
				Server.Users.Add(user);

				// 3. Check user key directory

				if (!string.IsNullOrEmpty(Config.UserPublicKeyDir))
				{
					if (!Directory.Exists(Config.UserPublicKeyDir))
					{
						Log.Write(LogColor.Important, "User public key directory '{0}' does not exist.", Config.UserPublicKeyDir);
					}
				}

				// 4. Register custom authentication handler

				Server.Authentication += Server_Authentication;

				// 5. Ready to start

				if (Config.AutoStart)
				{
					StartServer();
				}
				else
				{
					Log.Write("");
					Log.Write(@"   Press ""Start"" button to begin.");
					Log.Write("");
				}
			}
			catch (Exception ex)
			{
				Log.Write(ex);
			}

			UpdateUI();
		}

		private void Server_Authentication(object sender, AuthenticationEventArgs e)
		{
			if (e.UserName != Config.UserName)
			{
				e.Reject();
				return;
			}

			if (e.Key != null)
			{
				// test that the actual key is one of the expected keys
				var keys = UserPublicKeys;
				if (keys != null && keys.Contains(e.Key))
					e.Accept(Server.Users[Config.UserName]);
				else
					e.Reject();
			}
			else
			{
				if (e.Password == Config.UserPassword)
					e.Accept(Server.Users[Config.UserName]);
				else
					e.Reject();
			}
		}

		private void StartStopButton_Click(object sender, EventArgs e)
		{
			try
			{
				if (IsStarted)
				{
					StopServer();
				}
				else
				{
					StartServer();
				}
			}
			catch (Exception ex)
			{
				Log.Write(ex);
			}

			UpdateUI();
		}

		private void Configure()
		{
			UserPublicKeys = null;
			Server.Settings.AllowedAuthenticationMethods = AuthenticationMethods.Password;
			Server.Settings.KeepAlivePeriod = 180; // 3 minutes
			Server.Settings.SshParameters.SetEncryptionAlgorithms(Config.Ciphers);
			if (!string.IsNullOrEmpty(Config.UserPublicKeyDir))
			{
				// iterate through all public keys and load them
				var userPublicKeys = GetUserPublicKeys(Config.UserPublicKeyDir);
				if (userPublicKeys.Count == 0)
				{
					Log.Write(LogColor.Important, "No user public keys found in '{0}'.", Config.UserPublicKeyDir);
					Log.Write(LogColor.Important, "Public key authentication disabled.");
				}
				else
				{
					Log.Write(LogColor.Success, "Public key authentication enabled.");
					UserPublicKeys = userPublicKeys.ToArray();
					Server.Settings.AllowedAuthenticationMethods |= AuthenticationMethods.PublicKey;
				}
				Log.Write("");
			}
		}

		private void StartServer()
		{
			Configure();

			Log.Write("Binding SFTP server to port {0}...", Config.ServerPort);
			BindServer();

			if (Server.Bindings.Count == 0)
				return;

			Log.Write("Starting...");
			Server.Start();

			Log.Write(LogColor.Success, "SFTP server has started and is ready to accept connections.");
			IsStarted = true;
		}

		private void StopServer()
		{
			Log.Write("Stopping...");
			Server.Stop();
			Server.Unbind();

			Log.Write(LogColor.Success, "SFTP server is stopped.");
			IsStarted = false;
		}

		private void BindServer()
		{
			foreach (IPAddress ip in Config.IPAddresses)
			{
				BindProtocol(ip, FileServerProtocol.Sftp);
				BindProtocol(ip, FileServerProtocol.Shell);
			}
		}

		private void BindProtocol(IPAddress ip, FileServerProtocol protocol)
		{
			Exception ex = null;
			try
			{
				Server.Bind(new IPEndPoint(ip, Config.ServerPort), protocol);
			}
			catch (SocketException x)
			{
				ex = x;
			}
			catch (InvalidOperationException x)
			{
				ex = x;
			}
			if (ex != null)
			{
				Log.Write(LogColor.Error, "Unable to bind {3} protocol to port {0} on {1} interface: {2}", Config.ServerPort, ip, ex.Message, protocol);
				Log.Write(LogColor.Important, "Try changing 'sshPort' or 'sshIPBindings' in the configuration file.");
			}
		}

		private void UpdateUI()
		{
			if (IsStarted)
			{
				StartStopButton.Text = "&Stop";
			}
			else
			{
				StartStopButton.Text = "&Start";
			}
		}

		private SshPrivateKey LoadOrCreatePrivateKey(string filename, string password, bool useRsa)
		{
			if (!File.Exists(filename))
			{
				Log.Write(LogColor.Important, "Private key file '{0}' not found.", filename);
				Log.Write("Generating a new private key...");
				SshPrivateKey key;
				if (useRsa)
				{
					// generate a 2048-bit RSA key
					key = SshPrivateKey.Generate(SshHostKeyAlgorithm.RSA, 2048);
				}
				else
				{
					// generate a 1024-bit DSS key
					key = SshPrivateKey.Generate(SshHostKeyAlgorithm.DSS, 1024);
				}

				Log.Write("Saving the {1} key to '{0}'...", Path.GetFullPath(filename), useRsa ? "RSA" : "DSS");
				key.Save(filename, password, SshPrivateKeyFormat.Putty);

				return key;
			}

			return new SshPrivateKey(filename, password);
		}

		private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (MinimizeToTray && !IsExit)
			{
				e.Cancel = true;
				WindowState = FormWindowState.Minimized;
			}
			else
			{
				// stop the server first
				if (IsStarted)
				{
					StopServer();
				}
				NotifyIcon.Visible = false;
			}
		}

		private void MainForm_Resize(object sender, EventArgs e)
		{
			if (MinimizeToTray && WindowState == FormWindowState.Minimized)
			{
				ShowInTaskbar = false;
				Hide();
			}
		}

		private void NotifyIcon_Click(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				menuItemShow_Click(sender, e);
			}
		}

		private void menuItemShow_Click(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
			{
				Show();
				ShowInTaskbar = true;
				WindowState = FormWindowState.Normal;
			}
			Activate();
		}

		private void menuItemExit_Click(object sender, EventArgs e)
		{
			IsExit = true;
			Close();
		}

		private void LogLevelCombo_SelectionChangeCommitted(object sender, EventArgs e)
		{
			var level = (LogLevel)LogLevelCombo.SelectedValue;
			Log.Level = level;
			Log.Write("Log level changed to {0}.", level);
		}

		private void LinkToHomepage_Click(object sender, EventArgs e)
		{
			Process.Start(Config.HomepageUrl);
		}

		private static string RtfEscapeString(string value)
		{
			return value.Replace(@"\", @"\\");
		}

		private void label4_Click(object sender, EventArgs e)
		{
			Process.Start(Config.BuruServerUrl);
		}

		private void menuClearLog_Click(object sender, EventArgs e)
		{
			LogRichTextBox.Clear();
		}
	}
}
