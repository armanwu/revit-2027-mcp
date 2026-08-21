using System;
using Autodesk.Revit.UI;

namespace RevitBridge
{
    public class RevitBridgeApp : IExternalApplication
    {
        private HttpBridgeListener? _listener;
        private RevitExternalHandler? _externalHandler;
        private ExternalEvent? _externalEvent;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Initialize External Event Handler for thread-safe Revit UI execution
                _externalHandler = new RevitExternalHandler();
                _externalEvent = ExternalEvent.Create(_externalHandler);

                // Start background HTTP Listener on port 8000 automatically on Revit startup
                _listener = new HttpBridgeListener(_externalEvent, _externalHandler);
                _listener.Start();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RevitBridge Startup Error", $"Failed to initialize RevitBridge: {ex.Message}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _listener?.Stop();
                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }
    }
}
