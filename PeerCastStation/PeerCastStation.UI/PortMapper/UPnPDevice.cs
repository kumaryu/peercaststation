using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Xml.Linq;
using System.Net.NetworkInformation;
using PeerCastStation.Core;

namespace PeerCastStation.UI.PortMapper
{
  public class UPnPServiceDescription
  {
    public string DeviceName { get; private set; }
    public string DeviceType { get; private set; }
    public string UDN { get; private set; }
    public string ServiceId { get; private set; }
    public string ServiceType { get; private set; }
    public Uri ControlUrl { get; private set; }
    public Uri EventSubUrl { get; private set; }
    public Uri SCPDUrl { get; private set; }
    public UPnPServiceDescription(string device_name, string device_type, string udn, string service_id, string service_type, Uri control_url, Uri event_sub_url, Uri scpd_url)
    {
      this.DeviceName  = device_name;
      this.DeviceType  = device_type;
      this.UDN         = udn;
      this.ServiceId   = service_id;
      this.ServiceType = service_type;
      this.ControlUrl  = control_url;
      this.EventSubUrl = event_sub_url;
      this.SCPDUrl     = scpd_url;
    }
    public override bool Equals(object obj)
    {
      if (obj==null) return false;
      if (this.GetType()!=obj.GetType()) return false;
      if (ReferenceEquals(this, obj)) return true;
      var x = ((UPnPServiceDescription)obj);
      return this.UDN==x.UDN && this.ServiceId==x.ServiceId;
    }

    public override int GetHashCode()
    {
      if (String.IsNullOrEmpty(this.UDN) ||
          String.IsNullOrEmpty(this.ServiceId)) return 0;
      return (this.UDN+this.ServiceId).GetHashCode();
    }
  }

  public class UPnPService
  {
    private Logger logger;
    public UPnPServiceDescription ServiceDescription { get; private set; }
    public UPnPService(UPnPServiceDescription service_desc)
    {
      this.logger = new Logger(this.GetType());
      this.ServiceDescription = service_desc;
    }

    public class HttpErrorException
      : ApplicationException
    {
      public HttpStatusCode Code { get; private set; }

      public HttpErrorException(HttpStatusCode code, string message)
        : base(message)
      {
        this.Code = code;
      }
    }

    public class UPnPErrorException
      : ApplicationException
    {
      public int Code { get; private set; }

      public UPnPErrorException(int code, string message)
        : base(message)
      {
        this.Code = code;
      }
    }

    public class ActionResult
    {
      public string Action { get; private set; }
      public bool IsSucceeded { get; private set; }
      public Dictionary<string, string> Parameters { get; private set; }
      public Exception Exception { get; private set; }

      public ActionResult(string action, Dictionary<string, string> parameters)
      {
        this.IsSucceeded = true;
        this.Action = action;
        this.Parameters = parameters;
      }

      public ActionResult(string action, HttpStatusCode code, string description)
        : this(action, new HttpErrorException(code, description))
      {
      }

      public ActionResult(string action, int code, string description)
        : this(action, new UPnPErrorException(code, description))
      {
      }

      public ActionResult(string action, Exception exception)
      {
        this.IsSucceeded = false;
        this.Action = action;
        this.Exception = exception;
      }
    }

    private XDocument CreateActionRequest(string action, Dictionary<string,string> parameters)
    {
      var ns_s = XNamespace.Get("http://schemas.xmlsoap.org/soap/envelope/");
      var ns_u = XNamespace.Get(this.ServiceDescription.ServiceType);
      var doc = new XDocument(
        new XElement(ns_s+"Envelope",
          new XAttribute(XNamespace.Xmlns+"s", ns_s),
          new XElement(ns_s+"Body",
            new XElement(ns_u+action,
              new XAttribute(XNamespace.Xmlns+"u", ns_u),
              parameters.Select(kv => new XElement(XName.Get(kv.Key), kv.Value))
            )
          )
        )
      );
      return doc;
    }

    private async Task<ActionResult> ParseActionResponse(string action, HttpResponseMessage msg)
    {
      if (msg.IsSuccessStatusCode) {
        logger.Info("UPnP Action {0} Success", action);
        var doc = XDocument.Load(await msg.Content.ReadAsStreamAsync());
        var results = doc.Descendants()
          .Where(node => node.Parent!=null)
          .Where(node => node.Parent.Name==XName.Get(action+"Response", this.ServiceDescription.ServiceType));
        var parameters = new Dictionary<string, string>();
        foreach (var param in results) {
          logger.Debug("Param {0}:{1}", param.Name, param.Value);
          parameters.Add(param.Name.LocalName, param.Value);
        }
        return new ActionResult(action, parameters);
      }
      else if (msg.StatusCode==HttpStatusCode.InternalServerError) {
        var doc = XDocument.Load(await msg.Content.ReadAsStreamAsync());
        var error_code = doc.Descendants(XName.Get("errorCode", "urn:schemas-upnp-org:control-1-0")).Single();
        var error_description = doc.Descendants(XName.Get("errorDescription", "urn:schemas-upnp-org:control-1-0")).Single();
        logger.Info("UPnP Action {0} Error, code:{1}, descripion:{2}", action, error_code, error_description);
        return new ActionResult(action, Int32.Parse(error_code.Value), error_description.Value);
      }
      else {
        logger.Info("UPnP Action {0} Other Error, status code:{1}}", action, msg.StatusCode);
        return new ActionResult(action, msg.StatusCode, msg.ReasonPhrase);
      }
    }

    public async Task<ActionResult> SendActionAsync(string action, Dictionary<string, string> parameters, CancellationToken cancel_token)
    {
      try {
        var request = CreateActionRequest(action, parameters);
        var writer = new StringWriter();
        request.Save(writer);

        logger.Debug("Sending UPnP Action {0} to {1}", this.ServiceDescription.ServiceType+"#"+action, this.ServiceDescription.ControlUrl);
        try {
          var content = new StringContent(writer.ToString(), System.Text.Encoding.UTF8, "text/xml");
          content.Headers.Add("SOAPACTION", "\"" + this.ServiceDescription.ServiceType+"#"+action + "\"");
          using (var client=new HttpClient()) {
            return await ParseActionResponse(
                action,
                await client.PostAsync(this.ServiceDescription.ControlUrl, content, cancel_token));
          }
        }
        catch (Exception e) {
          logger.Debug("Send UPnP Action {0} failed:{1}", this.ServiceDescription.ServiceType+"#"+action, e);
          return new ActionResult(action, e);
        }
      }
      catch (OperationCanceledException e) {
        throw e;
      }
    }

    public Task<ActionResult> SendActionAsync(string action, CancellationToken cancel_token)
    {
      return SendActionAsync(action, new Dictionary<string, string>(), cancel_token);
    }

    public override bool Equals(object obj)
    {
      if (obj==null) return false;
      if (this.GetType()!=obj.GetType()) return false;
      if (ReferenceEquals(this, obj)) return true;
      var x = ((UPnPService)obj);
      return this.ServiceDescription.UDN==x.ServiceDescription.UDN &&
             this.ServiceDescription.ServiceId==x.ServiceDescription.ServiceId;
    }

    public override int GetHashCode()
    {
      if (String.IsNullOrEmpty(this.ServiceDescription.UDN) ||
          String.IsNullOrEmpty(this.ServiceDescription.ServiceId)) return 0;
      return (this.ServiceDescription.UDN+this.ServiceDescription.ServiceId).GetHashCode();
    }

    public override string ToString()
    {
      return String.Format("{0},{1}", ServiceDescription.DeviceName, ServiceDescription.ServiceId);
    }
  }

  public class WANConnectionService
    : UPnPService, INatDevice
  {
    public static readonly string PortMappingDescription = "PeerCastStation";

    public string Name {
      get { return this.ToString(); }
    }

    private bool IsOnSameNetwork(IPAddress mask, IPAddress addr1, IPAddress addr2)
    {
      if (addr1==null || addr2==null) return false;
      if (addr1==addr2) return true;
      if (mask.AddressFamily!=addr1.AddressFamily || mask.AddressFamily!=addr2.AddressFamily) return false;
      var bytes1 = mask.GetAddressBytes().Zip(addr1.GetAddressBytes(), (a,b) => a & b);
      var bytes2 = mask.GetAddressBytes().Zip(addr2.GetAddressBytes(), (a,b) => a & b);
      return bytes1.SequenceEqual(bytes2);
    }

    private async Task<IPAddress> GetInternalAddressAsync()
    {
      var dev_addr = (await Dns.GetHostAddressesAsync(ServiceDescription.ControlUrl.DnsSafeHost))
        .Where(addr => addr.AddressFamily==AddressFamily.InterNetwork)
        .First();
      return NetworkInterface.GetAllNetworkInterfaces()
        .Where(intf => intf.OperationalStatus==OperationalStatus.Up)
        .Select(intf => intf.GetIPProperties())
        .SelectMany(ipprop => ipprop.UnicastAddresses)
        .Where(addrinfo => addrinfo.Address.AddressFamily==AddressFamily.InterNetwork)
        .Where(addrinfo => IsOnSameNetwork(addrinfo.IPv4Mask, addrinfo.Address, dev_addr))
        .Select(addrinfo => addrinfo.Address)
        .FirstOrDefault();
    }

    public WANConnectionService(UPnPServiceDescription service_desc)
      : base(service_desc)
    {
    }

    public async Task<IPAddress> GetExternalAddressAsync(CancellationToken cancel_token)
    {
      var result = await SendActionAsync("GetExternalIPAddress", cancel_token);
      string value;
      IPAddress addr;
      if (result.IsSucceeded &&
          result.Parameters.TryGetValue("NewExternalIPAddress", out value) &&
          IPAddress.TryParse(value, out addr)) {
        return addr;
      }
      else {
        return null;
      }
    }

    public async Task<MappedPort> MapAsync(MappingProtocol protocol, int port, TimeSpan lifetime, CancellationToken cancel_token)
    {
      var parameters = new Dictionary<string,string>() {
        { "NewRemoteHost",     "" },
        { "NewExternalPort",   port.ToString() },
        { "NewProtocol",       protocol==MappingProtocol.TCP ? "TCP" : "UDP" },
        { "NewInternalPort",   port.ToString() },
        { "NewInternalClient", (await GetInternalAddressAsync()).ToString() },
        { "NewEnabled",        "1" },
        { "NewPortMappingDescription", PortMappingDescription },
        { "NewLeaseDuration",  "0" },
      };
      var result = await SendActionAsync("AddPortMapping", parameters, cancel_token);
      if (result.IsSucceeded) {
        if (lifetime==Timeout.InfiniteTimeSpan) {
          return new MappedPort(this, protocol, port, port, DateTime.Now+TimeSpan.FromSeconds(604800));
        }
        else {
          return new MappedPort(this, protocol, port, port, DateTime.Now+lifetime);
        }
      }
      else {
        return null;
      }
    }

    public async Task UnmapAsync(MappingProtocol protocol, int port, CancellationToken cancel_token)
    {
      var parameters = new Dictionary<string,string>() {
        { "NewRemoteHost",   "" },
        { "NewExternalPort", port.ToString() },
        { "NewProtocol",     protocol==MappingProtocol.TCP ? "TCP" : "UDP" },
      };
      await SendActionAsync("DeletePortMapping", parameters, cancel_token);
    }
  }

  public class WANCommonInterfaceConfigService
    : UPnPService
  {
    public WANCommonInterfaceConfigService(UPnPServiceDescription service_desc)
      : base(service_desc)
    {
    }

    public enum WANAccessType {
      Unknown = 0,
      POTS,
      DSL,
      Cable,
      Ethernet,
    }

    public enum PhysicalLinkStatus {
      Unknown = 0,
      Up,
      Down,
      Initializing,
      Unavailable,
    }

    public class CommonLinkProperties
    {
      public WANAccessType WANAccessType { get; private set; }
      public int Layer1UpstreamMaxBitRate { get; private set; }
      public int Layer1DownstreamMaxBitRate { get; private set; }
      public PhysicalLinkStatus PhysicalLinkStatus { get; private set; }
      public CommonLinkProperties(
        WANAccessType wan_access_type,
        int layer1_upstream_max_bitrate,
        int layer1_downstream_max_bitrate,
        PhysicalLinkStatus physical_link_status)
      {
        this.WANAccessType              = wan_access_type;
        this.Layer1UpstreamMaxBitRate   = layer1_upstream_max_bitrate;
        this.Layer1DownstreamMaxBitRate = layer1_downstream_max_bitrate;
        this.PhysicalLinkStatus         = physical_link_status;
      }
    }

    public async Task<CommonLinkProperties> GetCommonLinkProperties(CancellationToken cancel_token)
    {
      var result = await SendActionAsync("GetCommonLinkProperties", cancel_token);
      if (!result.IsSucceeded) return null;
      string value;
      WANAccessType wan_access_type = WANAccessType.Unknown;
      if (result.Parameters.TryGetValue("NewWANAccessType", out value)) Enum.TryParse(value, out wan_access_type);
      int layer1_upstream_max_bitrate = 0;
      if (result.Parameters.TryGetValue("NewLayer1UpstreamMaxBitRate", out value)) Int32.TryParse(value, out layer1_upstream_max_bitrate);
      int layer1_downstream_max_bitrate = 0;
      if (result.Parameters.TryGetValue("NewLayer1DownstreamMaxBitRate", out value)) Int32.TryParse(value, out layer1_downstream_max_bitrate);
      PhysicalLinkStatus physical_link_status = PhysicalLinkStatus.Unknown;
      if (result.Parameters.TryGetValue("NewPhysicalLinkStatus", out value)) Enum.TryParse(value, out physical_link_status);
      return new CommonLinkProperties(wan_access_type, layer1_upstream_max_bitrate, layer1_downstream_max_bitrate, physical_link_status);
    }
  }

  class SSDPDiscoverer
  {
    static readonly IPEndPoint SSDPEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
    private static readonly Logger logger = new Logger(typeof(SSDPDiscoverer));

    class SSDPResponse
    {
      public IPEndPoint RemoteEndPoint { get; private set; }
      public string Location { get { return GetValueOrNull("Location"); } }
      public string EXT      { get { return GetValueOrNull("EXT"); } }
      public string ST       { get { return GetValueOrNull("ST"); } }
      public string USN      { get { return GetValueOrNull("USN"); } }
      public string Server   { get { return GetValueOrNull("Server"); } }
      public Dictionary<string, string> Headers { get; private set; }

      private string GetValueOrNull(string key)
      {
        string value;
        if (Headers.TryGetValue(key.ToUpperInvariant(), out value)) {
          return value;
        }
        else {
          return null;
        }
      }

      public SSDPResponse(IPEndPoint remote_endpoint, Dictionary<string, string> headers)
      {
        this.RemoteEndPoint = remote_endpoint;
        this.Headers = headers;
      }

      public override bool Equals(object obj)
      {
        if (obj==null) return false;
        if (this.GetType()!=obj.GetType()) return false;
        if (ReferenceEquals(this, obj)) return true;
        return this.USN==((SSDPResponse)obj).USN;
      }

      public override int GetHashCode()
      {
        if (String.IsNullOrEmpty(this.USN)) return 0;
        return this.USN.GetHashCode();
      }
    }

    private async Task<IEnumerable<SSDPResponse>> SSDPAsync(IPAddress bind_addr, CancellationToken cancel_token)
    {
      var msg = System.Text.Encoding.ASCII.GetBytes(
        "M-SEARCH * HTTP/1.1\r\n" +
        "HOST: 239.255.255.250:1900\r\n" +
        "MAN: \"ssdp:discover\"\r\n" +
        "MX: 3\r\n" +
        "ST: ssdp:all\r\n\r\n"
      );
      var responses = new List<SSDPResponse>();
      using (var client=new UdpClient(new IPEndPoint(bind_addr, 0))) {
        for (int i=0; i<3; i++) {
          await client.SendAsync(msg, msg.Length, SSDPEndpoint);
        }
        if (client.Available==0 && !cancel_token.IsCancellationRequested) {
          await Task.Delay(1000, cancel_token);
        }
        while (client.Available>0 && !cancel_token.IsCancellationRequested) {
          var result = await client.ReceiveAsync();
          var response = System.Text.Encoding.ASCII.GetString(result.Buffer).Split(new string[] { "\r\n" }, StringSplitOptions.None);
          if (response.Length<0) continue;
          if (response[0].IndexOf("HTTP/1.1 200")!=0) continue;
          var header_pattern = new System.Text.RegularExpressions.Regex(@"(.*?):(.*)");
          var headers = new Dictionary<string,string>();
          foreach (var line in response.Skip(1)) {
            if (line.Length==0) break;
            var md = header_pattern.Match(line);
            if (!md.Success) continue;
            var key   = md.Groups[1].Value.Trim();
            var value = md.Groups[2].Value.Trim();
            headers.Add(key.ToUpperInvariant(), value);
          }
          var rsp = new SSDPResponse(result.RemoteEndPoint, headers);
          logger.Debug("SSDP Found {0} at {1}", rsp.ST, rsp.Location);
          responses.Add(rsp);
          if (client.Available==0 && !cancel_token.IsCancellationRequested) {
            await Task.Delay(100, cancel_token);
          }
        }
      }
      return responses.Distinct();
    }

    private IEnumerable<IPAddress> GetInternalAddresses()
    {
      return NetworkInterface.GetAllNetworkInterfaces()
        .Where(intf => intf.OperationalStatus==OperationalStatus.Up)
        .Select(intf => intf.GetIPProperties())
        .Where(ipprop => ipprop.UnicastAddresses.Count>0)
        .SelectMany(ipprop => ipprop.UnicastAddresses
            .Select(addr => addr.Address)
            .Where(addr => addr.AddressFamily==AddressFamily.InterNetwork));
    }

    private Uri MakeUrl(string base_url, string rel_url)
    {
      if (rel_url==null) return null;
      var url = new Uri(rel_url, UriKind.RelativeOrAbsolute);
      return url.IsAbsoluteUri ? url : new Uri(new Uri(base_url), url);
    }

    private static readonly XNamespace DeviceNS = "urn:schemas-upnp-org:device-1-0";
    public async Task<IEnumerable<UPnPServiceDescription>> GetUPnPServiceAsync(string location, CancellationToken cancel_token)
    {
      using (var client=new HttpClient()) {
        var response = await client.GetAsync(location, cancel_token);
        var doc = XDocument.Load(await response.Content.ReadAsStreamAsync());
        var urlbase = doc.Descendants(DeviceNS+"URLBase").SingleOrDefault();
        var baseurl = urlbase==null ? location : urlbase.Value;
        var devices = doc.Descendants(DeviceNS+"device");
        return devices.SelectMany(dev => {
          var friendly_name = dev.Elements(DeviceNS+"friendlyName").Select(elt => elt.Value).SingleOrDefault();
          var device_type   = dev.Elements(DeviceNS+"deviceType").Select(elt => elt.Value).SingleOrDefault();
          var udn           = dev.Elements(DeviceNS+"UDN").Select(elt => elt.Value).SingleOrDefault();
          return dev.Elements(DeviceNS+"serviceList").SelectMany(elt => elt.Elements(DeviceNS+"service")).Select(svc => {
            var service_type  = svc.Elements(DeviceNS+"serviceType").Select(elt => elt.Value).SingleOrDefault();
            var service_id    = svc.Elements(DeviceNS+"serviceId").Select(elt => elt.Value).SingleOrDefault();
            var control_url   = MakeUrl(baseurl, svc.Elements(DeviceNS+"controlURL").Select(elt => elt.Value).SingleOrDefault());
            var event_sub_url = MakeUrl(baseurl, svc.Elements(DeviceNS+"eventSubURL").Select(elt => elt.Value).SingleOrDefault());
            var scpd_url      = MakeUrl(baseurl, svc.Elements(DeviceNS+"SCPDURL").Select(elt => elt.Value).SingleOrDefault());
            return new UPnPServiceDescription(friendly_name, device_type, udn, service_id, service_type, control_url, event_sub_url, scpd_url);
          });
        });
      }
    }

    private async Task<IEnumerable<SSDPResponse>> SSDPAsync(CancellationToken cancel_token)
    {
      var results = Enumerable.Empty<SSDPResponse>();
      foreach (var task in GetInternalAddresses().Select(addr => SSDPAsync(addr, cancel_token))) {
        results = results.Concat(await task);
      }
      return results;
    }

    class SupportedService {
      public string Device;
      public string Service;
      public Type   Type;
    }
    static readonly SupportedService[] SupportedServices = {
      new SupportedService {
        Device="urn:schemas-upnp-org:device:WANConnectionDevice:1",
        Service="urn:schemas-upnp-org:service:WANPPPConnection:1",
        Type=typeof(WANConnectionService),
      },
      new SupportedService {
        Device="urn:schemas-upnp-org:device:WANConnectionDevice:1",
        Service="urn:schemas-upnp-org:service:WANIPConnection:1",
        Type=typeof(WANConnectionService),
      },
      new SupportedService {
        Device="urn:schemas-upnp-org:device:WANConnectionDevice:2",
        Service="urn:schemas-upnp-org:service:WANPPPConnection:1",
        Type=typeof(WANConnectionService),
      },
      new SupportedService {
        Device="urn:schemas-upnp-org:device:WANConnectionDevice:2",
        Service="urn:schemas-upnp-org:service:WANIPConnection:2",
        Type=typeof(WANConnectionService),
      },
      new SupportedService {
        Device="urn:schemas-upnp-org:device:WANDevice:1",
        Service="urn:schemas-upnp-org:service:WANCommonInterfaceConfig:1",
        Type=typeof(WANCommonInterfaceConfigService),
      },
      new SupportedService {
        Device="urn:schemas-upnp-org:device:WANDevice:2",
        Service="urn:schemas-upnp-org:service:WANCommonInterfaceConfig:1",
        Type=typeof(WANCommonInterfaceConfigService),
      },
    };

    static readonly Type[] constructorArgs = new Type[] { typeof(UPnPServiceDescription) };
    public async Task<IEnumerable<UPnPService>> DiscoverAsync(CancellationToken cancel_token)
    {
      var responses = (await SSDPAsync(cancel_token)).Distinct();
      var services = (await Task.WhenAll(
            responses.Select(async rsp => {
              try {
                return await GetUPnPServiceAsync(rsp.Location, cancel_token);
              }
              catch (OperationCanceledException) {
                throw;
              }
              catch (Exception) {
                return Enumerable.Empty<UPnPServiceDescription>();
              }
            })
          )
        )
        .SelectMany(svcs => svcs)
        .Distinct()
        .Select(svc => {
        var type = SupportedServices
          .Where(supported => supported.Device==svc.DeviceType && supported.Service==svc.ServiceType)
          .Select(supported => supported.Type)
          .FirstOrDefault() ?? typeof(UPnPService);
        return type.GetConstructor(constructorArgs).Invoke(new object[] { svc }) as UPnPService;
      });
      return services;
    }

  }

  public class UPnPWANConnectionServiceDiscoverer
    : INatDeviceDiscoverer
  {
    public async Task<IEnumerable<INatDevice>> DiscoverAsync(CancellationToken cancel_token)
    {
      var services = await (new SSDPDiscoverer()).DiscoverAsync(cancel_token);
      return services
        .Select(svc => svc as WANConnectionService)
        .Where(svc => svc!= null);
    }
  }

}
