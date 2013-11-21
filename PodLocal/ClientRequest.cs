using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PodLocal
{
    public class Command
    {
        public Command() { }
        public Command(string cmd)
        {
            command = cmd;
        }

        public string command;
    }

    public class BoolResponse
    {
        public BoolResponse() { }
        public BoolResponse(bool res)
        {
            result = res;
        }

        public bool result;
    }

    class ClientRequest
    {
        public ClientRequest() { }

        public string control;
        public string command;
        public string[] args;
        
        public object authenticate(ref bool hasAuthed)
        {
            if (control == "auth" && args.Length >= 2)
            {
                hasAuthed = Authentication.authenticate(command, args[0], args[1]);
                if (hasAuthed)
                    return new BoolResponse(true);
            }
            return new Command("authenticate");
        }

        public object process(uint ID) {
            //
            // TODO:: Dynamically load DLLs here
            //  Static function that allows registration of control name
            //  Then we do a hash lookup and send the command to them
            //
            //  Also there should be events we can tie into (update tray for example)
            //  Updated tray should have a hash of module names and save status against them. Then we can show all status at once for everything.
            //
            switch (control)
            {
                case "cam":

                    //
                    // All camera control must be done on a single thread
                    //
                    MethodInvoker action = delegate { PodLocal.self.camera.processCommand(command, args, ID); };
                    PodLocal.self.cameraStatus.BeginInvoke(action);
                    
                    return new BoolResponse(true);
                case "app":
                    if (args.Length >= 1)
                    {
                        AppLauncher.launch(command, String.Join(" ", args));
                    }
                    else {
                        AppLauncher.launch(command);
                    }
                    return new BoolResponse(true);
            }
            return new BoolResponse(false);
        }
    }
}
