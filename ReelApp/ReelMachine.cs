using Impinj.OctaneSdk;
using System;

namespace ReelApp
{
    internal abstract class ReelMachine:IDisposable
    {
        // Can only have 1 connection to the reader - Static Class Var
        internal static readonly ImpinjReader Reader = new ImpinjReader();

        internal abstract void PerformTagOperation();

        private void Reader_GpiChanged(ImpinjReader reader, GpiEvent e)
        {
            if (e.PortNumber == 1 && e.State)
            {
                // GPI1 has gone high - Begin Tag Operations on current tag
                // Set Busy Flag = BUSY
                reader.SetGpo(1, true);
                PerformTagOperation();
            }
        }

        internal void SignalNextTag()
        {
            // Set Busy Flag = Not BUSY
            Reader.SetGpo(1, false);
        }

        internal abstract Settings ConfigureSettings(Settings settings);

        internal void Start()
        {
            try
            {
                //Enable GPI1, so we can hear it trigger...
                Settings settings = Reader.QueryDefaultSettings();
                settings.Gpis.GpiConfigs[0].DebounceInMs = 50;
                settings.Gpis.GpiConfigs[0].IsEnabled = true;

                // Customize settings
                ConfigureSettings(settings);
                Reader.ApplySettings(settings);

                //Signal we're ready to go
                Reader.SetGpo(2, true);     // pass/fail signal (always pass, we'll record failure locally only)
                Reader.SetGpo(1, false);    // busy/ready signal (Initially set to NOT Busy?)
            } catch (Exception e)
            {
                Console.WriteLine($"Caught error: {e}");
                Console.WriteLine(e.StackTrace);
            }
        }

        public void Dispose()
        {
            try { Reader.Stop(); } catch (Exception) { };
            try { Reader.Disconnect(); } catch (Exception) { };
        }

        private ReelMachine() { }
        internal ReelMachine(string readerAddress)
        {
            try
            {
                Reader.Connect(readerAddress);
                Reader.GpiChanged += Reader_GpiChanged;
            }
            catch (OctaneSdkException e)
            {
                Console.WriteLine($"Caught error: {e}");
                Console.WriteLine(e.StackTrace);
            }
        }
    }
}
