using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Rebex.TinySftpServer
{
	/// <summary>
	/// Improved RichTextBox control.
	/// </summary>
	[DesignerCategory("")]
	public class RichTextBoxExtra : RichTextBox
	{
		private class LoggingMessage
		{
			public int CharsCount { get; set; }
			public string ColorRtf { get; set; }
			public string Rtf { get; set; }
		}

		private string _header =
			@"{\rtf1\ansi\ansicpg1252\deff0\deflang1033" + "\n" +
			@"{\fonttbl{\f0\fnil\fcharset0 Microsoft Sans Serif;}{\f1\fnil Courier New;}}" + "\n" +
			@"{\colortbl ;";
		private const string _defaults = "\n\\fs17 ";

		private readonly LinkedList<LoggingMessage> _messages;
		private readonly Dictionary<Color, string> _colors;
		private readonly StringBuilder _escape;
		private readonly StringBuilder _rtf;
		private bool _scheduled;

		/// <summary>
		/// Creates an instance of <see cref="RichTextBoxExtra"/>.
		/// </summary>
		public RichTextBoxExtra()
		{
			MaxCharsCount = 20 * 1000;
			_messages = new LinkedList<LoggingMessage>();
			_colors = new Dictionary<Color, string>();
			_escape = new StringBuilder();
			_rtf = new StringBuilder();
		}

		/// <summary>
		/// Maximum count of characters in the <see cref="RichTextBoxExtra"/>.
		/// </summary>
		[Description("Maximuml count of characters in the RichTextBoxExtra.")]
		public int MaxCharsCount { get; set; }

		/// <summary>
		/// Clears all text in the <see cref="RichTextBoxExtra"/>.
		/// </summary>
		public new void Clear()
		{
			lock (_messages)
			{
				_messages.Clear();
			}
			base.Clear();
		}

		/// <summary>
		/// Appends text using specified color.
		/// </summary>
		public void AppendLine(string text, Color color)
		{
			string colorRtf;
			lock (_colors)
			{
				// get color RTF or register new color
				if (!_colors.TryGetValue(color, out colorRtf))
				{
					_header += string.Format(@"\red{0}\green{1}\blue{2};", color.R, color.G, color.B);
					colorRtf = string.Format(@"\cf{0} ", _colors.Count + 1);
					_colors.Add(color, colorRtf);
				}
			}

			LoggingMessage msg;
			lock (_escape)
			{
				// prepare new message
				msg = new LoggingMessage()
				{
					CharsCount = text.Length,
					ColorRtf = colorRtf,
					Rtf = string.Format("{0}{1}\\par\n", colorRtf, EscapeRtf(text))
				};
			}

			lock (_messages)
			{
				_messages.AddLast(msg);

				// exit routine if update already scheduled
				if (_scheduled)
					return;

				// invoke is only possible once the underlying native control has been actually created
				if (Handle == IntPtr.Zero)
					return;

				_scheduled = true;
			}

			// schedule RTF update, while still accepting more messages,
			// improving the overall form responsiveness
			BeginInvoke(new Action(UpdateRtf));
		}

		/// <summary>
		/// Updates <see cref="RichTextBox.Rtf"/> with actual messages considering the <see cref="MaxCharsCount"/>.
		/// </summary>
		private void UpdateRtf()
		{
			lock (_messages)
			{
				try
				{
					RemoveExceedingMessages();

					_rtf.Length = 0;
					_rtf.Append(_header);
					_rtf.Append('}'); // end \colortbl
					_rtf.Append(_defaults);
					foreach (var msg in _messages)
					{
						_rtf.Append(msg.Rtf);
					}
					_rtf.Append('}'); // end \rtf1
				}
				finally
				{
					_scheduled = false;
				}
			}

			Rtf = _rtf.ToString();

			Select(TextLength, 0);
			ScrollToCaret();
		}

		/// <summary>
		/// Removes old messages to respect the <see cref="MaxCharsCount"/>.
		/// </summary>
		private void RemoveExceedingMessages()
		{
			// count total chars and keep messages not exceeding 'MaxCharsCount'
			int totalChars = 0;
			var node = _messages.Last; // the most recent message
			while (node != null)
			{
				var msg = node.Value;

				if (totalChars + msg.CharsCount > MaxCharsCount)
				{
					// try to shorten the message which exceeded the limit
					var stop = TryTrim(msg, MaxCharsCount - totalChars) ? node : node.Next;
					// remove extra messages
					while (_messages.First != stop)
						_messages.RemoveFirst();
				}

				totalChars += msg.CharsCount;
				node = node.Previous;
			}
		}

		private bool TryTrim(LoggingMessage msg, int maxLength)
		{
			if (MaxCharsCount > 256 && maxLength < 256) // consider only messages long enough
				return false;

			int rtfLength = msg.Rtf.Length;
			int lineIndex = msg.Rtf.IndexOf('\n', rtfLength - maxLength);
			if (lineIndex < 0 || lineIndex == rtfLength - 1)
				return false;

			msg.Rtf = msg.ColorRtf + msg.Rtf.Substring(lineIndex + 1);

			// Note:
			// We should count plaintext chars (without RTF control codes),
			// but it would be unnecessarily expensive operation.
			// It is enough to report count of RTF chars.
			// We only need to ensure that 'MaxCharsCount' is not exceeded.
			msg.CharsCount = rtfLength - lineIndex;
			return true;
		}

		/// <summary>
		/// Escapes RTF characters in a plaintext and encodes non-ASCII chars.
		/// </summary>
		private string EscapeRtf(string text)
		{
			_escape.Length = 0;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c >= 0x20 && c <= 0x7F) // ASCII printable chars
				{
					if (c == '\\' || c == '{' || c == '}')
						_escape.Append('\\');
					_escape.Append(c);
				}
				else if (c <= 0xFF) // 8-bit chars except ASCII printables
				{
					if (c == '\r')
						continue;
					if (c == '\n')
						_escape.Append("\\par\n");
					else
						_escape.AppendFormat("\\'{0:X2}", (int)c);
				}
				else // Unicode chars
				{
					_escape.AppendFormat("\\u{0}?", (int)c);
				}
			}
			return _escape.ToString();
		}
	}
}
