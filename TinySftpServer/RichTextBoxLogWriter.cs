using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rebex.TinySftpServer
{
	/// <summary>
	/// Implementation of Rebex.ILogWriter which logs to the specified RichTextBox.
	/// </summary>
	public class RichTextBoxLogWriter : LogWriterBase
	{
		private RichTextBox _textbox;
		private int _maxCharsCount;

		public RichTextBoxLogWriter(RichTextBox textbox, int maxCharsCount, LogLevel level)
		{
			_textbox = textbox;
			_maxCharsCount = maxCharsCount;
			Level = level;
		}

		public override void Write(LogLevel level, Type objectType, int objectId, string area, string message)
		{
			if (level < Level)
				return;

			Color color = LogColor.Info;

			if (level >= LogLevel.Error)
			{
				color = LogColor.Error;
			}
			else
			{
				switch (area.ToUpper())
				{
					case "COMMAND": color = LogColor.Command; break;
					case "RESPONSE": color = LogColor.Response; break;
					case "SSH": color = LogColor.Ssh; break;
					case "TLS": color = LogColor.Tls; break;
				}
			}

			message = string.Format("{0:HH:mm:ss.fff} {1} {2}: {3}",
				DateTime.Now, level.ToString(), area, message);

			try
			{
				if (!_textbox.IsDisposed)
					_textbox.Invoke(new WriteInnerDelegate(WriteInner), color, message);
			}
			catch (ObjectDisposedException)
			{
			}
		}

		public void Write(string message)
		{
			WriteInner(LogColor.Info, message);
		}

		public void Write(string format, params object[] args)
		{
			WriteInner(LogColor.Info, string.Format(format, args));
		}

		public void Write(Color color, string message)
		{
			WriteInner(color, message);
		}

		public void Write(Color color, string format, params object[] args)
		{
			WriteInner(color, string.Format(format, args));
		}

		public void Write(Exception ex)
		{
			if (ex is AggregateException)
			{
				foreach (Exception inner in ((AggregateException)ex).InnerExceptions)
				{
					Write(inner);
				}
				return;
			}

			WriteInner(LogColor.Error, "* " + ex);
		}

		private delegate void WriteInnerDelegate(Color color, string message);

		private void WriteInner(Color color, string message)
		{
			message += "\r\n";
			EnsureTextSpace(message.Length);

			_textbox.Focus();
			_textbox.SelectionColor = color;
			_textbox.AppendText(message);
		}

		private void EnsureTextSpace(int length)
		{
			if (_textbox.TextLength + length < _maxCharsCount)
				return;

			int spaceLeft = _maxCharsCount - length;

			if (spaceLeft <= 0)
			{
				_textbox.Clear();
				return;
			}

			string plainText = _textbox.Text;

			// find the end of line
			int start = plainText.IndexOf('\n', plainText.Length - spaceLeft);
			if (start >= 0 && start + 1 < plainText.Length)
			{
				_textbox.SelectionStart = 0;
				_textbox.SelectionLength = start + 1;

				// setting the SelectedText property is available only when ReadOnly = false
				bool ro = _textbox.ReadOnly;
				_textbox.ReadOnly = false;
				_textbox.SelectedText = "";
				_textbox.ReadOnly = ro;

				_textbox.SelectionStart = _textbox.TextLength;
				_textbox.SelectionLength = 0;
			}
			else
			{
				_textbox.Clear();
			}
		}
	}
}
