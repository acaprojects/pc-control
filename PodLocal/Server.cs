using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//
// For serialising and deserialising commands sent
//	and our server sockets
//
using Newtonsoft.Json;

using System.IO;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

using System.Windows.Forms; //MessageBox


namespace PodLocal
{

	public class Buffer {
		char[] delimiter;
		string buffer;

		public Buffer(char del = '\n') {
			buffer = "";
            delimiter = new char[] { del };
		}


        public string[] subArray(string[] data, int index, int length)
        {
            string[] result = new string[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        //
        // Breaks the string up into the individual responses and returns complete responses as an array
        //  Any incomplete responses remain in the buffer.
        //
		public string[] extract(string data) {
			buffer += data;
			string[] tokens = buffer.Split(delimiter);
            if (tokens[0] == buffer)
                return new string[0];

            buffer = tokens[tokens.Length - 1];
            tokens = subArray(tokens, 0, tokens.Length - 1);

			return tokens;
		}
	}

	class Server
	{
        public static SecureTcpServer server = null;
        private static string start = Convert.ToChar(2).ToString();
        private static string end = Convert.ToChar(3).ToString();

        private static int connections = 0;
        private static uint nextID = 1;
        private static Dictionary<uint, StreamWriter> currentConnections = new Dictionary<uint, StreamWriter>();
        private static PodLocal.UpdateTray updateStatus;

        private static readonly object _locker = new object();

        //
        // TODO:: provide an event hook for providing out of turn information to all the control systems connected
        //

        public Server(PodLocal.UpdateTray function)
        {
            if (server == null) // Singleton
            {
                updateStatus += function;
                int port = 443;


                string certPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                certPath = Path.Combine(certPath, "serverCert.cer");
                string localPath = System.Reflection.Assembly.GetEntryAssembly().Location;
                localPath = Path.GetDirectoryName(localPath);

                try
                {
                    //certPath = Path.Combine(certPath, "serverCert.cer");
                    X509Certificate serverCert = X509Certificate.CreateFromCertFile(certPath);

                    server = new SecureTcpServer(port, serverCert,
                        new SecureConnectionResultsCallback(OnServerConnectionAvailable));
                    lock (_locker)
                    {
                        doStatusUpdate();
                    }
                    server.StartListening();
                }
                catch (System.Net.Sockets.SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.AddressAlreadyInUse) {
                        MessageBox.Show("Application already running. Socket in use.");
                        System.Environment.Exit(-1);
                    }
                }
                catch
                {
                    try {
                        System.Diagnostics.Process.Start(Path.Combine(localPath, "makecert.exe"), "-r -pe -n \"CN=MachineName_SS\" -ss my -a sha1 -sky exchange -eku 1.3.6.1.5.5.7.3.1 -sp \"Microsoft RSA SChannel Cryptographic Provider\" -sy 12 \"" + certPath + "\"");
                    } catch {
                        DialogResult res = MessageBox.Show("Encryption Failed. Generating TLS key file...\nRetry or cancel to exit.", "Security Notice", MessageBoxButtons.RetryCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1);
                        updateStatus("Server", "TLS key file not found", true);
                        if (res == DialogResult.Retry)
                        {
                            Application.Exit();
                            return;
                        }
                        else
                        {
                            System.Environment.Exit(-1);
                            return;
                        }
                    }
                    Thread.Sleep(4000);
                    Application.Exit();
                }
            }
		}

        public static void sendObject(object response, uint id = 0) {
            try
            {
                string data = start + JsonConvert.SerializeObject(response) + end;
                lock (_locker)
                {
                    if (id == 0)
                    {
                        foreach (KeyValuePair<uint, StreamWriter> conection in currentConnections)
                        {
                            try
                            {
                                conection.Value.Write(data);
                            }
                            catch { }
                        }
                    }
                    else {
                        currentConnections[id].Write(data);
                    }
                }
            }
            catch { }
        }

        static void doStatusUpdate(string error = "") {
            if (connections == 0)
                if(error != "")
                    updateStatus("Server", "Error: " + error, false);
                else
                    updateStatus("Server", "Error: no controller connected", false);
            else
                updateStatus("Server", "Online: " + Server.connections.ToString() + " controller(s) connected", false);
        }

        static async void OnServerConnectionAvailable(object sender, SecureConnectionResults args)
		{
			if (args.AsyncException != null)
			{
                lock (_locker)
                {
                    doStatusUpdate(args.AsyncException.Message);
                }
				return;
			}

            uint myID;

			SslStream stream = args.SecureStream;
			StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
			writer.AutoFlush = true;
			StreamReader reader = new StreamReader(stream, Encoding.UTF8);

            Buffer buff = new Buffer(end[0]);
            string[] recieved;

            //
            // Count the connections
            //
            lock (_locker)
            {
                myID = nextID;
                nextID++;
                currentConnections.Add(myID, writer);

                connections++;
                doStatusUpdate();
            }

            //
            // Request authentication
            //
            try
            {
                await writer.WriteAsync(start + JsonConvert.SerializeObject(new Command("authenticate")) + end);

                bool authenticated = false;
                char[] response = new char[4096];
                int responseSize;


                while (true)
                {
                    try
                    {
                        //
                        // Read in the data
                        //
                        responseSize = await reader.ReadAsync(response, 0, 4096);
                        if (responseSize == 0)
                            break;  // Disconnect has occured

                        //
                        // Extract complete responses from the stream and process them
                        //
                        recieved = buff.extract(new string(response, 0, responseSize));
                        foreach (string item in recieved)
                        {
                            string final;
                            object ret;

                            try
                            {
                                //
                                // Remove the start character
                                //
                                final = (item.Split(new char[] { start[0] }))[1];

                                //
                                // Process client response
                                //
                                ClientRequest request = JsonConvert.DeserializeObject<ClientRequest>(final);
                                if (authenticated)
                                {
                                    // 
                                    // Process response
                                    //
                                    ret = request.process(myID);
                                }
                                else
                                {
                                    //
                                    // Attempt to Authenticate
                                    //
                                    ret = request.authenticate(ref authenticated);
                                    if (authenticated)   // Request camera status
                                    {
                                        MethodInvoker action = delegate
                                        {
                                            PodLocal.self.camera.sendStatus(myID);
                                        };
                                        PodLocal.self.cameraStatus.BeginInvoke(action);
                                    }
                                }

                                await writer.WriteAsync(start + JsonConvert.SerializeObject(ret) + end);
                            }
                            catch
                            {
                                //
                                // TODO:: Log these errors somewhere
                                //
                                //Console.WriteLine("Server error while processing response.");
                                break;
                            }
                        }
                    }
                    catch
                    {
                        //
                        // TODO:: Log these errors somewhere
                        //
                        //Console.WriteLine("Server error!");
                        break;
                    }
                }
            }
            catch {
                // grab any errors occuring at init
            }
            finally
            {
                lock (_locker)
                {
                    currentConnections.Remove(myID);
                    connections--;
                    doStatusUpdate();
                }
                writer.Close();
                reader.Close();
                stream.Close();
            }
		}
	}
}
