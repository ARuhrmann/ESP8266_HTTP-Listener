using System;
using System.Text;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Data.SqlClient;
using System.Security.Cryptography;

namespace CopagoTime
{
    public class TimeClock
    {

        public TimeClock(Employee employee)
        {
            int Anfangs_Puffer = -5;
            bool Atomzeit_Ermittelt = false;
            DateTime Zeitpunkt = this.GetNetworkTime(Atomzeit_Ermittelt);

            if (Atomzeit_Ermittelt == false)
            {
                Zeitpunkt = DateTime.Now;
            }

            bool sqlcon_Fehler = false;
            SqlConnection con = new SqlConnection(mod_SQL.SQL_ConStr);

            try
            {
                con.Open();
            }
            catch (Exception ex)
            {
                sqlcon_Fehler = true;
            }


            if (!sqlcon_Fehler)
            {
                SqlCommand cmd = new SqlCommand("Select PersonalNr, KartenNr, StempelzeitenNr, Kommt From CPG_Stempelzeiten_temp WHERE BenutzerID = " + employee.BenutzerID, con);
                DataTable tblStempelzeiten_temp = new DataTable();
                SqlDataReader r = cmd.ExecuteReader();
                tblStempelzeiten_temp.Load(r);



                // Neue Buchung erzeugen
                if (tblStempelzeiten_temp.Rows.Count == 0)
                {

                    string s = "Select count(*) From CPG_Stempelzeiten WHERE cast(Kommt As Date) = '" + Zeitpunkt.ToString("yyyy-MM-dd") + "' AND BenutzerID = " + employee.BenutzerID;
                    cmd.CommandText = s;
                    int Erste_Tagesbuchung =(int) cmd.ExecuteScalar();
                          
                    if (Erste_Tagesbuchung != 0)
                    {
                        Anfangs_Puffer = 0;
                    }
                    
                    s = "Insert into CPG_Stempelzeiten (BenutzerID, PersonalNr, KartenNr, Kommt) Values (";
                    s = s + " " + employee.BenutzerID;
                    s = s + ", " + employee.Personalnummer;
                    s = s + ", " + employee.Kartennummer;
                    s = s + ", '" + Zeitpunkt.AddMinutes(Anfangs_Puffer).ToString("yyyy-MM-dd HH:mm:ss") + "') ";
                    cmd.CommandText = s;
                    cmd.ExecuteNonQuery();

                    s = "Select MAX(id) From CPG_Stempelzeiten WHERE BenutzerID = " + employee.BenutzerID;
                    cmd.CommandText = s;
                    int StempelzeitenNr = (int) cmd.ExecuteScalar();

                    
                    s = "Insert into CPG_Stempelzeiten_temp (BenutzerID, PersonalNr, KartenNr,StempelzeitenNr, Kommt) Values (";
                    s = s + " " + employee.BenutzerID;
                    s = s + ", " + employee.Personalnummer;
                    s = s + ", " + employee.Kartennummer;
                    s = s + ", " + StempelzeitenNr;
                    s = s + ", '" + Zeitpunkt.AddMinutes(Anfangs_Puffer).ToString("yyyy-MM-dd HH:mm:ss") + "') ";
                    cmd.CommandText = s;
                    cmd.ExecuteNonQuery();

                    s = "Insert into CPG_Stempelzeiten_Festschreibung (id, ZeitpunktK, HashK) Values (";
                    s = s + " " + StempelzeitenNr;
                    s = s + ", '" + Zeitpunkt.ToString("yyyyMMdd HH:mm:ss") + "' ";
                    s = s + ", '" + get_Festschreibung_K(StempelzeitenNr, employee.BenutzerID, Zeitpunkt.AddMinutes(Anfangs_Puffer)) + "') ";
                    cmd.CommandText = s;
                    cmd.ExecuteNonQuery();
                    
                    // Buchung vervollständigen
                }
                else if (tblStempelzeiten_temp.Rows.Count == 1)
                {

                    int StempelzeitenNr = tblStempelzeiten_temp.Rows(0).Item("StempelzeitenNr");
                    DateTime kommt = DateTime.MinValue;
                    bool kommt_OK = DateTime.TryParse(tblStempelzeiten_temp.Rows(0).Item("kommt"), kommt);


                    if (kommt_OK)
                    {
                        int dauer = ((TimeSpan) (Zeitpunkt - kommt)).Seconds;

                        string s = "Update CPG_Stempelzeiten set";
                        s = s + " Geht = '" + Zeitpunkt.ToString("yyyy-MM-dd HH:mm:ss") + "' ";
                        s = s + ", Dauer = " + dauer + " ";
                        s = s + " WHERE id = " + StempelzeitenNr;
                        cmd.CommandText = s;


                        if (cmd.ExecuteNonQuery() == 1)
                        {
                            cmd.CommandText = "Delete From CPG_Stempelzeiten_temp WHERE StempelzeitenNr = " + StempelzeitenNr;
                            cmd.ExecuteNonQuery();

                            s = "Update CPG_Stempelzeiten_Festschreibung set ";
                            s = s + " ZeitpunktKG ='" + Zeitpunkt.ToString("yyyyMMdd HH:mm:ss") + "' ";
                            s = s + ", HashKG = '" + get_Festschreibung_KG(StempelzeitenNr, employee.BenutzerID, kommt, Zeitpunkt, dauer) + "' ";
                            s = s + " WHERE id = " + StempelzeitenNr;
                            cmd.CommandText = s;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        //public DateTime Ermittel_Atom_Zeit(ref bool Atomzeit_Ermittelt)
        //{

        //    Atomzeit_Ermittelt = false;
        //    DateTime AtomDateTime;

        //    List<string> l_NTPServer = new List<string>();
        //    l_NTPServer.Add("ptbtime1.ptb.de");
        //    l_NTPServer.Add("ptbtime2.ptb.de");
        //    l_NTPServer.Add("ptbtime3.ptb.de");


        //    foreach (string NTPServer_loopVariable in l_NTPServer)
        //    {
        //        NTPServer = NTPServer_loopVariable;

        //        try
        //        {
        //            dynamic ntpData = new byte[48];
        //            ntpData(0) = 0x1b;
        //            dynamic addresses = Dns.GetHostEntry(NTPServer).AddressList;
        //            dynamic ipEndPoint = new IPEndPoint(addresses(0), 123);
        //            dynamic socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //            socket.Connect(ipEndPoint);
        //            socket.Send(ntpData);
        //            socket.Receive(ntpData);
        //            socket.Close();
        //            const byte serverReplyTime = 40;
        //            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
        //            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);
        //            intPart = SwapEndianness(intPart);
        //            fractPart = SwapEndianness(fractPart);
        //            dynamic milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
        //            AtomDateTime = (new DateTime(1900, 1, 1)).AddMilliseconds(Convert.ToInt64(milliseconds));
        //            AtomDateTime = AtomDateTime.AddHours(1);
        //            // Aktuelle Zeitzone

        //            // Sommerzeit
        //            if (AtomDateTime.IsDaylightSavingTime() == true)
        //            {
        //                AtomDateTime = AtomDateTime.AddHours(1);
        //            }

        //            Atomzeit_Ermittelt = true;

        //            break; // TODO: might not be correct. Was : Exit For

        //        }
        //        catch (Exception ex)
        //        {
        //            Interaction.MsgBox(ex.Message);
        //        }

        //    }
        //    return AtomDateTime;
        //}

        public DateTime GetNetworkTime(){ return GetNetworkTime(true); }

        public DateTime GetNetworkTime(bool Atomzeit_Ermittelt)
        {
            //default Windows time server
            const string ntpServer = "time.windows.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;

            //The UDP port number assigned to NTP is 123
            var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //NTP uses UDP

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);

                //Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 3000;

                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime.ToLocalTime();
        }

        // stackoverflow.com/a/3294698/162671
        private static uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
        
        public string get_Festschreibung_K(int id, int BenutzerID, System.DateTime Kommt)
        {

            string @return = "";
            @return = MD5StringHash(id + BenutzerID + Kommt.ToString("yyyy-MM-dd HH:mm:ss") + "uhuurinal");

            return @return;

        }

        public object get_Festschreibung_KG(int id, int BenutzerID, System.DateTime Kommt, System.DateTime Geht, int Dauer)
        {

            string @return = "";
            @return = MD5StringHash(id + BenutzerID + Kommt.ToString("yyyy-MM-dd HH:mm:ss") + Geht.ToString("yyyy-MM-dd HH:mm:ss") + Dauer + "uhuurinal");

            return @return;

        }

        private string MD5StringHash(string input)
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();

            byte[] originalBytes = ASCIIEncoding.Default.GetBytes(input);
            byte[] encodedBytes = md5.ComputeHash(originalBytes);

            return BitConverter.ToString(encodedBytes).Replace("-", "");
        }

        //public string MD5StringHash(string strString)
        //{

        //    MD5CryptoServiceProvider MD5 = new MD5CryptoServiceProvider();
        //    byte[] Data = null;
        //    byte[] Result = null;
        //    string Res = "";
        //    string Tmp = "";
        //    byte[] Result = MD5.ComputeHash(Encoding.ASCII.GetBytes(strString));

        //    for (int i = 0; i <= Result.Length - 1; i++)
        //    {
        //        Tmp = Convert.Hex(Result(i));

        //        if (Tmp.Length == 1)
        //            Tmp = "0" + Tmp;

        //        Res += Tmp;
        //    }
        //    return Res;
        //}
    }
}
