using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

using DirectShowLib;

namespace PodLocal
{
	public class CamControl
	{
        private PodLocal.UpdateTray updateStatus;
        private PodLocal.UpdateCameraList updateCamList;
        private int camIndex = 0;
        private static readonly object _locker = new object();


        public CamControl(PodLocal.UpdateTray status, PodLocal.UpdateCameraList cameras)
        {
            updateStatus += status;
            updateCamList += cameras;

            lock (_locker)
            {
                reloadCamera();
            }
		}


		//
		// Camera control structures
		//
		IAMCameraControl theCamera;
        public class CamData
		{
            public CamData(string typ) {
                type = typ;
                device = "cam";
            }

			public int min;
			public int step;
			public int max;
			public int value;

            public string type;
            public string device;
		}

		CamData pan = new CamData("pan");
        CamData tilt = new CamData("tilt");
        CamData zoom = new CamData("zoom");
        CamData focus = new CamData("focus");	    // Not really using this
		public static bool cameraReady = false;
        CameraControlFlags flags;                   // Not used, just required


		public void processCommand(string command, string[] args, uint id)
		{
            lock (_locker)
            {
                if (!cameraReady)
                    reloadCamera();

                try
                {
                    switch (command)
                    {
                        case "status":
                            sendStatus(id);
                            break;
                        case "up":
                            up();
                            Server.sendObject(tilt);    // Send to all
                            break;
                        case "down":
                            down();
                            Server.sendObject(tilt);    // Send to all
                            break;
                        case "left":
                            left();
                            Server.sendObject(pan);    // Send to all
                            break;
                        case "right":
                            right();
                            Server.sendObject(pan);    // Send to all
                            break;
                        case "center":
                            center();
                            Server.sendObject(tilt);    // Send to all
                            Server.sendObject(pan);    // Send to all
                            break;
                        case "zoomin":
                            zoomin();
                            Server.sendObject(zoom);    // Send to all
                            break;
                        case "zoomout":
                            zoomout();
                            Server.sendObject(zoom);    // Send to all
                            break;
                        case "zoom":
                            dozoom(Int32.Parse(args[0]));
                            Server.sendObject(zoom);    // Send to all
                            break;
                    }
                }
                catch
                {
                    cameraReady = false;    // Reconnect to the camera
                    reloadCamera();
                }
            }
		}


        public void sendStatus(uint id = 0) {
            Server.sendObject(tilt, id);
            Server.sendObject(pan, id);
            Server.sendObject(zoom, id);
        }


		public void reloadCamera(int index = 0)
		{
            lock (_locker)
            {

                camIndex = index;

                try
                {
                    //
                    // Get the camera devices
                    //  TODO:: Provide the control system with a list of cameras to select from
                    //  TODO:: Provide a right click menu for selecting the camera to be controlled
                    //  TODO:: Event for sending out-of-turn status information to the control system
                    //
                    DsDevice[] capDevices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

                    //
                    // Get the graphbuilder object
                    //
                    IFilterGraph2 graphBuilder = new FilterGraph() as IFilterGraph2;

                    //
                    // add the video input device
                    //
                    IBaseFilter camFilter = null;
                    int hr = graphBuilder.AddSourceFilterForMoniker(capDevices[camIndex].Mon, null, capDevices[camIndex].Name, out camFilter);
                    DsError.ThrowExceptionForHR(hr);

                    //
                    // Camera control object
                    //
                    theCamera = camFilter as IAMCameraControl;
                    getProperties();
                    updateStatus("Camera", "Selected: " + capDevices[camIndex].Name, false);

                    if (!cameraReady)
                    {
                        cameraReady = true;
                        string[] lstCameras = new string[capDevices.Length];
                        for (int i = 0; i < capDevices.Length; i++)
                        {
                            lstCameras[i] = capDevices[i].Name;
                        }
                        updateCamList(lstCameras, camIndex);
                    }
                }
                catch
                {
                    cameraReady = false;
                    updateStatus("Camera", "Error: unable to bind controls", false);
                }
                
            }
		}

		private void GetControlProperties(CameraControlProperty CtrlProp, out int iMin, out int iMax, out int iStep)
		{
			int iDft;
			CameraControlFlags flags;
			int iResult = theCamera.GetRange(CtrlProp, out iMin, out iMax, out iStep, out iDft, out flags);
		}

		public void getProperties()
		{
			GetControlProperties(CameraControlProperty.Pan, out pan.min, out pan.max, out pan.step);
			GetControlProperties(CameraControlProperty.Tilt, out tilt.min, out tilt.max, out tilt.step);
			GetControlProperties(CameraControlProperty.Zoom, out zoom.min, out zoom.max, out zoom.step);
			GetControlProperties(CameraControlProperty.Focus, out focus.min, out focus.max, out focus.step);

			theCamera.Get(CameraControlProperty.Pan, out pan.value, out flags);
			theCamera.Get(CameraControlProperty.Tilt, out tilt.value, out flags);
			theCamera.Get(CameraControlProperty.Zoom, out zoom.value, out flags);
			theCamera.Get(CameraControlProperty.Focus, out focus.value, out flags);
		}

		int iResult;

		public void up() {
			iResult = theCamera.Set(CameraControlProperty.Tilt, tilt.value + tilt.step, CameraControlFlags.Manual);
			theCamera.Get(CameraControlProperty.Tilt, out tilt.value, out flags);
		}

		public void down()
		{
			iResult = theCamera.Set(CameraControlProperty.Tilt, tilt.value - tilt.step, CameraControlFlags.Manual);
			theCamera.Get(CameraControlProperty.Tilt, out tilt.value, out flags);
		}

		public void left()
		{
			iResult = theCamera.Set(CameraControlProperty.Pan, pan.value + pan.step, CameraControlFlags.Manual);
			theCamera.Get(CameraControlProperty.Pan, out pan.value, out flags);
		}

		public void right()
		{
			iResult = theCamera.Set(CameraControlProperty.Pan, pan.value - pan.step, CameraControlFlags.Manual);
			theCamera.Get(CameraControlProperty.Pan, out pan.value, out flags);
		}

		public void center()
		{
			theCamera.Set(CameraControlProperty.Pan, (pan.min + pan.max) / 2, CameraControlFlags.Manual);
			theCamera.Get(CameraControlProperty.Pan, out pan.value, out flags);
			theCamera.Set(CameraControlProperty.Tilt, (tilt.min + tilt.max) / 2, CameraControlFlags.Manual);
			theCamera.Get(CameraControlProperty.Tilt, out tilt.value, out flags);
		}

		public void zoomin()
		{
			iResult = theCamera.Set(CameraControlProperty.Zoom, zoom.value + zoom.step, (CameraControlFlags)0L);	// absolute
			theCamera.Get(CameraControlProperty.Zoom, out zoom.value, out flags);
		}

		public void zoomout()
		{
			iResult = theCamera.Set(CameraControlProperty.Zoom, zoom.value - zoom.step, (CameraControlFlags)0L);	// absolute
			theCamera.Get(CameraControlProperty.Zoom, out zoom.value, out flags);
		}

		public void dozoom(int val)
		{
			iResult = theCamera.Set(CameraControlProperty.Zoom, val, (CameraControlFlags)0L);	// absolute
			theCamera.Get(CameraControlProperty.Zoom, out zoom.value, out flags);
		}
	}
}
