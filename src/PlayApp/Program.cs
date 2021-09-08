using System;
using System.Reflection;
using System.Threading.Tasks;
using Dapper;
using HarmonyLib;
using Kerberos.NET.Client;
using Kerberos.NET.Configuration;
using Kerberos.NET.Credentials;
using Microsoft.Data.SqlClient;

namespace PlayApp
{
    internal class Program
    {
        private const string SqlServerAddress = "ad.almirex.com";
        private const string ClientUsername = "iwaclient@ALMIREX.DC";
        private const string ClientPassword = "P@ssw0rd";
        private const string SqlServerSPN = "MSSQLSvc/dc-controller.almirex.dc"; // you MUST create an SPN in this form or SQL server will reject the ticket
        private const string KdcAddress = "ad.almirex.com";
        public static void Main(string[] args)
        {
            var harmony = new Harmony("demo");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            for (int i = 0; i < 1; i++)
            {
                try
                {
                    var connection = new SqlConnection($"Server={SqlServerAddress};Database=master;Encrypt=False;Integrated Security=true;");
                    // var connection = new SqlConnection($"Server={SqlServerAddress};Database=master;User Id=sa;Password=New0rder;Encrypt=False");
                    connection.Open();
                    var result = connection.ExecuteScalar<int>("select 123");
                    // result = connection.ExecuteScalar<int>("select 123");
                    Console.WriteLine($"OPEN! {result}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        
        public static async Task<byte[]> GetTicket()
        {
            var kerbCredential = new KerberosPasswordCredential("iwaclient", ClientPassword, "ALMIREX.DC");
            var kerbConfig = Krb5Config.Default();
            kerbConfig.Realms[kerbCredential.Domain].Kdc.Add(KdcAddress);
            var kerbClient = new KerberosClient(kerbConfig);
            await kerbClient.Authenticate(kerbCredential);
            var ticket = await kerbClient.GetServiceTicket(SqlServerSPN);
      
            var ticketBytes =  ticket.EncodeGssApi().ToArray();
            var ticketBase64 = Convert.ToBase64String(ticketBytes);
            Console.WriteLine($"Ticket: {ticketBase64}");
            return ticketBytes;
        }
    }

    [Harmony]
    public class SNISSPIDataPatch
    {
        static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Microsoft.Data.SqlClient.TdsParser"), "SNISSPIData");

        static bool Prefix(byte[] sendBuff, ref uint sendLength)
        {
            // byte[] token = Program.GetTicket().Result;
            // token.CopyTo(sendBuff,0);
            // sendLength = (uint)token.Length;
            return true;
        }
    
        static void Postfix(byte[] sendBuff, ref uint sendLength)
        {
            // var ticket = new byte[sendLength];
            // sendBuff.CopyTo(ticket, sendLength);
            var ticket = new Span<byte>(sendBuff, 0, (int)sendLength);
            var ticketBase64 = Convert.ToBase64String(ticket.ToArray());
            // Console.WriteLine($"Original Ticket: {ticketBase64}");
        }
    
    }
}
