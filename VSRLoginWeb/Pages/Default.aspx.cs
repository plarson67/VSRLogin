using System;
using System.Linq;
using System.Web;
using System.ComponentModel;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Security.Cryptography;
using System.Text;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;
 
namespace VSRLoginWeb
{
    [ToolboxItemAttribute(false)]
    /// <summary>
    /// Contains my site's global variables.
    /// </summary>
     
    public partial class Default : System.Web.UI.Page
    {
        static string _url;

        /// <summary>
        /// Get or set the static important data.
        /// </summary>
        public static string Url
        {
            get
            {
                return _url;
            }
            set
            {
                _url = value;
            }
        }
        public class UserProperties
        {
            public string SessionID { get; set; }
            public string LoginName { get; set; }
            public string DisplayName { get; set; }
            public string Role { get; set; }
            public string Email { get; set; }
            public string ApplicationID { get; set; }
            public string PortalID { get; set; }
            public string IPAddress { get; set; }
            public string WebBrowserName { get; set; }
        }
        private string AES256Encrypt(string text)
        {
            AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
            string AesIV256 = @"!QAZ2WSX#EDC4RFV";
            string AesKey256 = @"5TGB&YHN7UJM(IK<5TGB&YHN7UJM(IK<";
            aes.BlockSize = 128;
            aes.KeySize = 256;
            aes.IV = Encoding.UTF8.GetBytes(AesIV256);
            aes.Key = Encoding.UTF8.GetBytes(AesKey256);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            byte[] src = Encoding.Unicode.GetBytes(text);
            using (ICryptoTransform encrypt = aes.CreateEncryptor())
            {
                byte[] dest = encrypt.TransformFinalBlock(src, 0, src.Length);
                return Convert.ToBase64String(dest);
            }
        }
        public string GetIPAddress()
        {
            var IPAddress = string.Empty;
            try
            {
                System.Web.HttpContext context = System.Web.HttpContext.Current;
                string ipAddress = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];

                if (!string.IsNullOrEmpty(ipAddress))
                {
                    string[] addresses = ipAddress.Split(',');
                    if (addresses.Length != 0)
                    {
                        return addresses[0];
                    }
                }
                return context.Request.ServerVariables["REMOTE_ADDR"];
            }
            catch (Exception ex)
            {
                HttpContext.Current.Response.Write("Exception: " + ex.GetType().Name + "; Error Message: " + ex.Message);
            }
            return IPAddress;
        }
        public string GetSessionID(string userCredential)
        {
            //DIRAuthServiceClient client = new DIRAuthServiceClient();
            Binding binding = new BasicHttpBinding();
            EndpointAddress endpointAddress = new EndpointAddress("http://204.67.170.72:8080/DIRAuthService.svc?wsdl");
            try
            {

                VSRService.DIRAuthServiceClient client = new VSRService.DIRAuthServiceClient(binding, endpointAddress);

                string results = client.GetSession(userCredential);
                client.Close();
                return results;
            }
            catch (FaultException<VSRService.ErrorDetail> error)
            {
                HttpContext.Current.Response.Write("Error Code: " + error.Detail.errorCode + "; Error Message: " + error.Detail.errorMessage);
                return null;
            }
        }
        protected bool IsMemberOfGroup(string groupName)
        {
            bool isMember = false;

            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            var groupNames = from id in identity.Groups
                             select id.Translate(typeof(NTAccount)).Value.ToLower();

            foreach (string group in groupNames)
            {
                if (group.Contains(groupName))
                {
                    isMember = true;
                    break;
                }  
            }
            return isMember;
        }
        protected void Page_PreInit(object sender, EventArgs e)
        {
            Uri redirectUrl;
            switch (SharePointContextProvider.CheckRedirectionStatus(Context, out redirectUrl))
            {
                case RedirectionStatus.Ok:
                    return;
                case RedirectionStatus.ShouldRedirect:
                    Response.Redirect(redirectUrl.AbsoluteUri, endResponse: true);
                    break;
                case RedirectionStatus.CanNotRedirect:
                    Response.Write("An error occurred while processing your request.");
                    Response.End();
                    break;
            }
        }
        protected void Page_Load(object sender, EventArgs e)
        {
            // The following code gets the client context and Title property by using TokenHelper.
            // To access other properties, the app may need to request permissions on the host web.
            var spContext = SharePointContextProvider.Current.GetSharePointContext(Context);

            using (var clientContext = spContext.CreateUserClientContextForSPHost())
            {
                //bool isMemberOfAdmin = IsMemberOfGroup("vsr admins");
                bool isMemberOfAdmin = IsMemberOfGroup("spfarmadmins");  
                bool isMemberOfUser = IsMemberOfGroup("vsr users");
                bool isMemberOfVendor = IsMemberOfGroup("vsr vendors");
                string UserRole = "";
                if (isMemberOfAdmin)
                {
                    UserRole = "DIR_Admin";
                }
                else if (isMemberOfUser)
                {
                    UserRole = "DIR_User";
                }
                else if (isMemberOfVendor)
                {
                    UserRole = "Vendor";
                }
                else
                {
                    throw new ApplicationException("Access denied - You are not authorized to access VSR application.");
                }

                
                string IPAddr = GetIPAddress() + "Z";
                var requestBase = HttpContext.Current.Request;
                string BrowserName = string.Empty;
                if (requestBase != null)
                {
                    BrowserName = requestBase.Browser.Browser + "Z";
                }
                else
                {
                    BrowserName = "Unknown" + "Z";
                }
                string AppID = AES256Encrypt("DIRVSRApplication"); 
                UserProperties CurrentUserInfo = new UserProperties();
                clientContext.Load(clientContext.Web, web => web.Title);
                clientContext.ExecuteQuery();
                string userName = WindowsIdentity.GetCurrent().Name;
                string email = UserPrincipal.Current.EmailAddress;
                string DisplayName = UserPrincipal.Current.DisplayName;


                //userName = Environment.UserName;
                CurrentUserInfo.SessionID = "SessionIDWindows";
                CurrentUserInfo.LoginName = userName;
                //SPPrincipalInfo prinInfo = SPUtility.ResolvePrincipal(web, CurrentUserInfo.LoginName, SPPrincipalType.All, SPPrincipalSource.All, null, false);
                CurrentUserInfo.DisplayName = DisplayName;
                CurrentUserInfo.Role = UserRole;
                CurrentUserInfo.Email = email;
                CurrentUserInfo.ApplicationID = AppID;
                CurrentUserInfo.PortalID = "Vendor";
                CurrentUserInfo.IPAddress = IPAddr;
                CurrentUserInfo.WebBrowserName = BrowserName;
                 
                string s = GetSessionID(XMLUtility.CreateXML(CurrentUserInfo));
                //Response.Write("SessionID: " + s + "<br>");
 
                Url = "https://qa.vsr.dir.texas.gov/home/Index?sessionid=" + s ;
               
                //Response.Write("<a target=_blank href='" + Url + "'>DIR VSR Portal</a>");
                //System.Web.HttpContext.Current.Response.Redirect(url, false);
                
                //VSRService objService = new VSRLoginWeb.VSRService(); 
            }
        }
    }
}

 