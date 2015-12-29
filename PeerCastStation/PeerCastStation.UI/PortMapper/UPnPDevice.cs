using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Linq;
using System.Net.NetworkInformation;

namespace PeerCastStation.UI.PortMapper
{
  public class UPnPDevice
    : INatDevice
  {
    public static readonly string PortMappingDescription = "PeerCastStation";
    private string location;
    public string ServiceType { get; private set; }
    public string USN { get; private set; }
    public Guid DeviceUUID { get; private set; }
    public IPAddress DeviceAddress { get; private set; }

    private IPAddress GetInternalAddress()
    {
      return NetworkInterface.GetAllNetworkInterfaces()
        .Where(intf => intf.OperationalStatus==OperationalStatus.Up)
        .Select(intf => intf.GetIPProperties())
        .Where(ipprop => ipprop.GatewayAddresses.Any(addr => addr.Address.Equals(this.DeviceAddress)))
        .Where(ipprop => ipprop.UnicastAddresses.Count>0)
        .SelectMany(ipprop => ipprop.UnicastAddresses
            .Select(addr => addr.Address)
            .Where(addr => addr.AddressFamily==AddressFamily.InterNetwork))
        .FirstOrDefault();
    }

    private static readonly System.Text.RegularExpressions.Regex UUIDPattern =
      new System.Text.RegularExpressions.Regex(@"^uuid:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private Guid GetUUIDFromUSN(string usn)
    {
      Guid result = Guid.Empty;
      var md = UUIDPattern.Match(usn);
      if (md.Success &&
          Guid.TryParse(md.Groups[1].Value, out result)) {
        return result;
      }
      else {
        return Guid.Empty;
      }
    }

    public UPnPDevice(IPAddress device_address, string location, string st, string usn)
    {
      this.DeviceAddress = device_address;
      this.location = location;
      this.ServiceType = st;
      this.USN = usn;
      this.DeviceUUID = GetUUIDFromUSN(usn);
    }

    private Uri controlPointUrl = null;
    private async Task<Uri> GetControlPointUrl(CancellationToken cancel_token)
    {
      if (controlPointUrl!=null) return controlPointUrl;
      using (var client=new HttpClient()) {
        var response = await client.GetAsync(this.location, cancel_token);
        if (!response.IsSuccessStatusCode) {
          throw new HttpErrorException(response.StatusCode, response.ReasonPhrase);
        }
        var doc = XDocument.Load(await response.Content.ReadAsStreamAsync());
        var urlbase = doc.Descendants()
          .Where(node => node.Name==XName.Get("URLBase", "urn:schemas-upnp-org:device-1-0"))
          .SingleOrDefault();
        var baseurl = urlbase==null ? this.location : urlbase.Value;
        var control_url = doc.Descendants()
          .Where(node => node.Parent!=null)
          .Where(node => node.Name==XName.Get("controlURL", "urn:schemas-upnp-org:device-1-0"))
          .Where(node => node.Parent.Descendants().Any(
            sub => sub.Name==XName.Get("serviceType", "urn:schemas-upnp-org:device-1-0") &&
                   sub.Value==this.ServiceType))
          .SingleOrDefault();
        if (control_url==null) {
          throw new InvalidDataException();
        }
        var url = new Uri(control_url.Value, UriKind.RelativeOrAbsolute);
        if (url.IsAbsoluteUri) {
          controlPointUrl = url;
        }
        else {
          controlPointUrl = new Uri(new Uri(this.location), url);
        }
        return controlPointUrl;
      }
    }

    public string Name {
      get { return location; }
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

    private class ActionResult
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

    private XmlDocument CreateActionRequest(string action, Dictionary<string,string> parameters)
    {
      var doc = new XmlDocument();
      var root = doc.CreateElement("s:Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
      root.SetAttribute("s:encodingStyle", "http://schemas.xmlsoap.org/soap/encoding/");
      var body = doc.CreateElement("s:Body", "http://schemas.xmlsoap.org/soap/envelope/");
      var act = doc.CreateElement("u", action, this.ServiceType);
      foreach (var kv in parameters) {
        var param = doc.CreateElement(kv.Key);
        param.AppendChild(doc.CreateTextNode(kv.Value));
        act.AppendChild(param);
      }
      body.AppendChild(act);
      root.AppendChild(body);
      doc.AppendChild(root);
      return doc;
    }

    private async Task<ActionResult> ParseActionResponse(string action, HttpResponseMessage msg)
    {
      if (msg.IsSuccessStatusCode) {
        var doc = XDocument.Load(await msg.Content.ReadAsStreamAsync());
        var results = doc.Descendants()
          .Where(node => node.Parent!=null)
          .Where(node => node.Parent.Name==XName.Get(action+"Response", this.ServiceType));
        var parameters = new Dictionary<string, string>();
        foreach (var param in results) {
          parameters.Add(param.Name.LocalName, param.Value);
        }
        return new ActionResult(action, parameters);
      }
      else if (msg.StatusCode==HttpStatusCode.InternalServerError) {
        var doc = XDocument.Load(await msg.Content.ReadAsStreamAsync());
        var error_code = doc.Descendants()
          .Where(node => node.Name==XName.Get("errorCode", "urn:schemas-upnp-org:control-1-0"))
          .Single();
        var error_description = doc.Descendants()
          .Where(node => node.Name==XName.Get("errorDescription", "urn:schemas-upnp-org:control-1-0"))
          .Single();
        return new ActionResult(action, Int32.Parse(error_code.Value), error_description.Value);
      }
      else {
        return new ActionResult(action, msg.StatusCode, msg.ReasonPhrase);
      }
    }

    private async Task<ActionResult> SendActionAsync(string action, Dictionary<string, string> parameters, CancellationToken cancel_token)
    {
      try {
        var controlpoint = await GetControlPointUrl(cancel_token);
        var request = CreateActionRequest(action, parameters);
        var writer = new StringWriter();
        request.Save(writer);

        var content = new StringContent(writer.ToString(), System.Text.Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPACTION", this.ServiceType+"#"+action);

        using (var client=new HttpClient()) {
          return await ParseActionResponse(
              action,
              await client.PostAsync(controlpoint, content, cancel_token));
        }
      }
      catch (OperationCanceledException e) {
        throw e;
      }
      catch (Exception e) {
        return new ActionResult(action, e);
      }
    }

    private Task<ActionResult> SendActionAsync(string action, CancellationToken cancel_token)
    {
      return SendActionAsync(action, new Dictionary<string, string>(), cancel_token);
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
        { "NewInternalClient", GetInternalAddress().ToString() },
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

    public override bool Equals(object obj)
    {
      if (obj==null) return false;
      if (this.GetType()!=obj.GetType()) return false;
      if (ReferenceEquals(this, obj)) return true;
      return this.USN==((UPnPDevice)obj).USN;
    }

    public override int GetHashCode()
    {
      if (String.IsNullOrEmpty(this.USN)) return 0;
      return this.USN.GetHashCode();
    }
  }

  public class UPnPDeviceDiscoverer
    : INatDeviceDiscoverer
  {
    static readonly IPEndPoint SSDPEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);

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

    private async Task<IEnumerable<SSDPResponse>> SSDPAsync(CancellationToken cancel_token)
    {
      var msg = System.Text.Encoding.ASCII.GetBytes(
        "M-SEARCH * HTTP/1.1\r\n" +
        "HOST: 239.255.255.250:1900\r\n" +
        "MAN: \"ssdp:discover\"\r\n" +
        "MX: 3\r\n" +
        "ST: ssdp:all\r\n\r\n"
      );
      var responses = new List<SSDPResponse>();
      using (var client=new UdpClient()) {
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
          responses.Add(new SSDPResponse(result.RemoteEndPoint, headers));
        }
      }
      return responses.Distinct();
    }

    class SupportedService {
      public string Service;
      public int    Priority;
    }
    static readonly SupportedService[] SupportedServices = {
      new SupportedService { Service="urn:schemas-upnp-org:service:WANIPConnection:1", Priority=1 },
      new SupportedService { Service="urn:schemas-upnp-org:service:WANPPPConnection:1", Priority=0 },
    };

    public async Task<IEnumerable<INatDevice>> DiscoverAsync(CancellationToken cancel_token)
    {
      var responses = await SSDPAsync(cancel_token);
      return responses
        .Where(rsp => SupportedServices.Any(svc => svc.Service==rsp.ST))
        .Select(rsp => new UPnPDevice(rsp.RemoteEndPoint.Address, rsp.Location, rsp.ST, rsp.USN))
        .GroupBy(dev => dev.DeviceUUID)
        .Select(group => group.OrderByDescending(dev => SupportedServices.First(svc => svc.Service==dev.ServiceType).Priority).First());
    }

  }

}
