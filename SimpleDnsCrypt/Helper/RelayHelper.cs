using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using System;
using System.Collections.Generic;
using System.IO;
using SimpleDnsCrypt.Utils;
using SimpleDnsCrypt.Utils.Models;

namespace SimpleDnsCrypt.Helper
{
    public static class RelayHelper
    {
        private static readonly ILog Log = LogManagerHelper.Factory();

        public static List<StampFileEntry> GetRelays()
        {
            var relays = new List<StampFileEntry>();
            var relayFile = Path.Combine(Global.DnsCryptFolderPath, "relays.md");
            try
            {
                if (File.Exists(relayFile))
                {
                    relays = StampTools.ReadStampFileEntries(relayFile);
                }
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
            return relays;
        }
    }
}
