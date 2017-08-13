using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.ComponentModel;

namespace HTTP_Server
{
    public class Server : INotifyPropertyChanged
    {
        private int counter;
        private HttpListener Listener;
        HttpListenerContext Context;
        String RequestInput;
        public event PropertyChangedEventHandler PropertyChanged;

        private String serverOutput = "test bestanden";

        public String ServerOutput
        {
            get { return serverOutput;  }
            set {
                Console.WriteLine(value);
                serverOutput = value;
                NotifyPropertyChange("ServerOutput");
            }
        }

        private void NotifyPropertyChange(string propName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propName));
        }

        public Server()
        {
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://localhost:8080/");
            Console.WriteLine("Listening..");

            Listener.Start();
            Listener.BeginGetContext(new AsyncCallback(ListenerCallback), Listener);
        }

        public void Stop()
        {
            this.Listener.Stop();
        }

        private void ListenerCallback(IAsyncResult Result)
        {
            try
            {
                Context = Listener.EndGetContext(Result);
                RequestInput = Context.Request.ToString();
                DisplayWebHeaderCollection(Context.Request);
                this.ProcessRequest(Context);
            }
            catch (HttpListenerException eR)
            {
                System.Diagnostics.Debug.WriteLine(eR.ToString());
            }
            try
            {
                Listener.BeginGetContext(new AsyncCallback(ListenerCallback), Listener);
            }
            catch (InvalidOperationException eR)
            {
                System.Diagnostics.Debug.WriteLine(eR.ToString());
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest Request = context.Request;
            const string responseString = "<html><body>Hello world</body></html>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);            
            context.Response.Close();
        }

        private void DisplayWebHeaderCollection(HttpListenerRequest request)
        {
            System.Collections.Specialized.NameValueCollection headers = request.Headers;
            StringBuilder serverOutput = new StringBuilder();
            // Get each header and display each value.
            foreach (string key in headers.AllKeys)
            {
                string[] values = headers.GetValues(key);
                if (values.Length > 0)
                {
                    Console.WriteLine("The values of the {0} header are: \n", key);
                    serverOutput.Append(String.Format("The values of the {0} header are: \n", key));
                    foreach (string value in values)
                    {
                        Console.WriteLine("   {0}", value);
                        serverOutput.Append(String.Format("   {0}", value));
                    }
                }
                else
                    Console.WriteLine("There is no value associated with the header.\n");
                    serverOutput.Append("There is no value associated with the header.\n");
            }
            this.ServerOutput = serverOutput.ToString();
        }
    }
}
