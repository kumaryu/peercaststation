using System;
using System.Net;

namespace PeerCastStation.Core.Http
{
  public static class HttpReasonPhrase
  {
    public const string Continue                     = "Continue";
    public const string SwitchingProtocols          = "Switching Protocols";
    public const string OK                          = "OK";
    public const string Created                     = "Created";
    public const string Accepted                    = "Accepted";
    public const string NonAuthoritativeInformation = "Non-Authoritative Information";
    public const string NoContent                   = "No Content";
    public const string ResetContent                = "Reset Content";
    public const string PartialContent              = "Partial Content";
    public const string MultipleChoices             = "Multiple Choices";
    public const string MovedPermanently            = "Moved Permanently";
    public const string Found                       = "Found";
    public const string SeeOther                    = "See Other";
    public const string NotModified                 = "Not Modified";
    public const string UseProxy                    = "Use Proxy";
    public const string TemporaryRedirect           = "Temporary Redirect";
    public const string BadRequest                  = "Bad Request";
    public const string Unauthorized                = "Unauthorized";
    public const string PaymentRequired             = "Payment Required";
    public const string Forbidden                   = "Forbidden";
    public const string NotFound                    = "Not Found";
    public const string MethodNotAllowed            = "Method Not Allowed";
    public const string NotAcceptable               = "Not Acceptable";
    public const string ProxyAuthenticationRequired = "Proxy Authentication Required";
    public const string RequestTimeout              = "Request Timeout";
    public const string Conflict                    = "Conflict";
    public const string Gone                        = "Gone";
    public const string LengthRequired              = "Length Required";
    public const string PreconditionFailed          = "Precondition Failed";
    public const string PayloadTooLarge             = "Payload Too Large";
    public const string URITooLong                  = "URI Too Long";
    public const string UnsupportedMediaType        = "Unsupported Media Type";
    public const string RangeNotSatisfiable         = "Range Not Satisfiable";
    public const string ExpectationFailed           = "Expectation Failed";
    public const string UpgradeRequired             = "Upgrade Required";
    public const string InternalServerError         = "Internal Server Error";
    public const string NotImplemented              = "Not Implemented";
    public const string BadGateway                  = "Bad Gateway";
    public const string ServiceUnavailable          = "Service Unavailable";
    public const string GatewayTimeout              = "Gateway Timeout";
    public const string HTTPVersionNotSupported     = "HTTP Version Not Supported";

    public static string GetReasonPhrase(int statusCode)
    {
      switch (statusCode) {
      case 100: return Continue;
      case 101: return SwitchingProtocols;
      case 200: return OK;
      case 201: return Created;
      case 202: return Accepted;
      case 203: return NonAuthoritativeInformation;
      case 204: return NoContent;
      case 205: return ResetContent;
      case 206: return PartialContent;
      case 300: return MultipleChoices;
      case 301: return MovedPermanently;
      case 302: return Found;
      case 303: return SeeOther;
      case 304: return NotModified;
      case 305: return UseProxy;
      case 307: return TemporaryRedirect;
      case 400: return BadRequest;
      case 401: return Unauthorized;
      case 402: return PaymentRequired;
      case 403: return Forbidden;
      case 404: return NotFound;
      case 405: return MethodNotAllowed;
      case 406: return NotAcceptable;
      case 407: return ProxyAuthenticationRequired;
      case 408: return RequestTimeout;
      case 409: return Conflict;
      case 410: return Gone;
      case 411: return LengthRequired;
      case 412: return PreconditionFailed;
      case 413: return PayloadTooLarge;
      case 414: return URITooLong;
      case 415: return UnsupportedMediaType;
      case 416: return RangeNotSatisfiable;
      case 417: return ExpectationFailed;
      case 426: return UpgradeRequired;
      case 500: return InternalServerError;
      case 501: return NotImplemented;
      case 502: return BadGateway;
      case 503: return ServiceUnavailable;
      case 504: return GatewayTimeout;
      case 505: return HTTPVersionNotSupported;
      default: throw new ArgumentOutOfRangeException(nameof(statusCode));
      }
    }

    public static string GetReasonPhrase(HttpStatusCode statusCode)
    {
      return GetReasonPhrase((int)statusCode);
    }
  }

}
