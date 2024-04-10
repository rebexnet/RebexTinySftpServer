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
		private RichTextBoxExtra _textbox;

		public RichTextBoxLogWriter(RichTextBoxExtra textbox, LogLevel level)
		{
			_textbox = textbox;
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
			_textbox.AppendLine(message, color);
		}
	}
}
