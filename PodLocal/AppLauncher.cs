using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PodLocal
{
	class AppLauncher
	{
        public static void launch(string page, string args = "")
		{
            if (page != "desktop")
                Process.Start(page, args);
            else {
                Shell32.Shell objShel = new Shell32.Shell();
                ((Shell32.IShellDispatch4)objShel).ToggleDesktop();
            }
		}
	}
}
