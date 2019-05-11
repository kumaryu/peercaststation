using System;
using System.Net;

namespace PeerCastStation.Core.Http
{
  public class HttpErrorException : ApplicationException
  {
    public HttpStatusCode StatusCode { get; private set; }
    public HttpErrorException(HttpStatusCode code)
      : base(StatusMessage(code))
    {
      StatusCode = code;
    }

    public HttpErrorException(HttpStatusCode code, string message)
      : base(message)
    {
      StatusCode = code;
    }

    private static string StatusMessage(HttpStatusCode code)
    {
      return code.ToString();
    }
  }
}
