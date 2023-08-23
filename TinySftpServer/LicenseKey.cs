using System;

namespace Rebex
{
    internal static class LicenseKey
    {
		/// <summary>
		/// To compile Tiny SFTP Server, a Rebex File Server license is required.
		/// Trial mode: Get your 30-day trial license key at https://www.rebex.net/support/trial/
		/// Full mode: Purchase Rebex File Server license at https://www.rebex.net/file-server/
		/// </summary>
		public static string Value
		{
			get
			{
				string key = null; // put your license key here

				// if no key is set, try reading it from REBEX_KEY environment variable.
				return key ?? Environment.GetEnvironmentVariable("REBEX_KEY");
			}
		}
	}
}
