using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//
// Required for a secure server
//
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;


namespace PodLocal
{
	class SecureTcpServer : IDisposable
	{
		X509Certificate serverCert;
		RemoteCertificateValidationCallback certValidationCallback;
		SecureConnectionResultsCallback connectionCallback;
		AsyncCallback onAcceptConnection;
		AsyncCallback onAuthenticateAsServer;

		bool started;

		int listenPort;
		TcpListener listenerV4;
		TcpListener listenerV6;
		int disposed;
		bool clientCertificateRequired;
		bool checkCertifcateRevocation;
		SslProtocols sslProtocols;

		public SecureTcpServer(int listenPort, X509Certificate serverCertificate,
		    SecureConnectionResultsCallback callback)
			: this(listenPort, serverCertificate, callback, null)
		{
		}

		public SecureTcpServer(int listenPort, X509Certificate serverCertificate,
		    SecureConnectionResultsCallback callback,
		    RemoteCertificateValidationCallback certValidationCallback)
		{
			if (listenPort < 0 || listenPort > UInt16.MaxValue)
				throw new ArgumentOutOfRangeException("listenPort");

			if (serverCertificate == null)
				throw new ArgumentNullException("serverCertificate");

			if (callback == null)
				throw new ArgumentNullException("callback");

			onAcceptConnection = new AsyncCallback(OnAcceptConnection);
			onAuthenticateAsServer = new AsyncCallback(OnAuthenticateAsServer);

			this.serverCert = serverCertificate;
			this.certValidationCallback = certValidationCallback;
			this.connectionCallback = callback;
			this.listenPort = listenPort;
			this.disposed = 0;
			this.checkCertifcateRevocation = false;
			this.clientCertificateRequired = false;
			this.sslProtocols = SslProtocols.Default;
		}

		~SecureTcpServer()
		{
			Dispose();
		}

		public SslProtocols SslProtocols
		{
			get { return sslProtocols; }
			set { sslProtocols = value; }
		}

		public bool CheckCertifcateRevocation
		{
			get { return checkCertifcateRevocation; }
			set { checkCertifcateRevocation = value; }
		}


		public bool ClientCertificateRequired
		{
			get { return clientCertificateRequired; }
			set { clientCertificateRequired = value; }
		}

		public void StartListening()
		{
			if (started)
				throw new InvalidOperationException("Already started...");

			IPEndPoint localIP;
			if (Socket.OSSupportsIPv4 && listenerV4 == null)
			{
				localIP = new IPEndPoint(IPAddress.Any, listenPort);
				Console.WriteLine("SecureTcpServer: Started listening on {0}", localIP);
				listenerV4 = new TcpListener(localIP);
			}

			if (Socket.OSSupportsIPv6 && listenerV6 == null)
			{
				localIP = new IPEndPoint(IPAddress.IPv6Any, listenPort);
				Console.WriteLine("SecureTcpServer: Started listening on {0}", localIP);
				listenerV6 = new TcpListener(localIP);
			}

			if (listenerV4 != null)
			{
				listenerV4.Start();
				listenerV4.BeginAcceptTcpClient(onAcceptConnection, listenerV4);
			}

			if (listenerV6 != null)
			{
				listenerV6.Start();
				listenerV6.BeginAcceptTcpClient(onAcceptConnection, listenerV6);
			}

			started = true;
		}

		public void StopListening()
		{
			if (!started)
				return;

			started = false;

			if (listenerV4 != null)
				listenerV4.Stop();
			if (listenerV6 != null)
				listenerV6.Stop();
		}

		void OnAcceptConnection(IAsyncResult result)
		{
			TcpListener listener = result.AsyncState as TcpListener;
			TcpClient client = null;
			SslStream sslStream = null;

			try
			{
				if (this.started)
				{
					//start accepting the next connection...
					listener.BeginAcceptTcpClient(this.onAcceptConnection, listener);
				}
				else
				{
					//someone called Stop() - don't call EndAcceptTcpClient because
					//it will throw an ObjectDisposedException
					return;
				}

				//complete the last operation...
				client = listener.EndAcceptTcpClient(result);


				bool leaveStreamOpen = false;//close the socket when done

				if (this.certValidationCallback != null)
					sslStream = new SslStream(client.GetStream(), leaveStreamOpen, this.certValidationCallback);
				else
					sslStream = new SslStream(client.GetStream(), leaveStreamOpen);

				sslStream.BeginAuthenticateAsServer(this.serverCert,
				    this.clientCertificateRequired,
				    this.sslProtocols,
				    this.checkCertifcateRevocation,//checkCertifcateRevocation
				    this.onAuthenticateAsServer,
				    sslStream);


			}
			catch (Exception ex)
			{
				if (sslStream != null)
				{
					sslStream.Dispose();
					sslStream = null;
				}
				this.connectionCallback(this, new SecureConnectionResults(ex));
			}
		}

		void OnAuthenticateAsServer(IAsyncResult result)
		{
			SslStream sslStream = null;
			try
			{
				sslStream = result.AsyncState as SslStream;
				sslStream.EndAuthenticateAsServer(result);

				this.connectionCallback(this, new SecureConnectionResults(sslStream));
			}
			catch (Exception ex)
			{
				if (sslStream != null)
				{
					sslStream.Dispose();
					sslStream = null;
				}

				this.connectionCallback(this, new SecureConnectionResults(ex));
			}
		}

		public void Dispose()
		{
			if (System.Threading.Interlocked.Increment(ref disposed) == 1)
			{
				if (this.listenerV4 != null)
					listenerV4.Stop();
				if (this.listenerV6 != null)
					listenerV6.Stop();

				listenerV4 = null;
				listenerV6 = null;

				GC.SuppressFinalize(this);
			}
		}
	}
}
