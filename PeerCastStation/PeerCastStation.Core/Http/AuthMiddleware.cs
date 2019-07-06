using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Owin;

namespace PeerCastStation.Core.Http
{
  public class AuthMiddleware
  {
    private Func<IDictionary<string, object>, Task> nextApp;
    private OutputStreamType acceptType;

    public AuthMiddleware(Func<IDictionary<string, object>, Task> nextApp, OutputStreamType acceptType)
    {
      this.nextApp = nextApp;
      this.acceptType = acceptType;
    }

    private string GetAuthorizationToken(OwinEnvironment env)
    {
      string result = null;
      var auth = env.GetRequestHeader("Authorization", (string)null);
      if (auth!=null) {
        var md = System.Text.RegularExpressions.Regex.Match(
          auth,
          @"\s*Basic (\S+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (md.Success) {
          result = md.Groups[1].Value;
        }
      }
      if (result==null) {
        var query = env.GetQueryParameters();
        query.TryGetValue("auth", out result);
      }
      if (result==null) {
        var cookies = env.GetRequestCookies();
        cookies.TryGetValue("auth", out result);
      }
      return result;
    }

    private bool CheckAuthorization(string authorization_token, AccessControlInfo acinfo)
    {
      if (!acinfo.AuthorizationRequired || acinfo.AuthenticationKey==null) return true;
      if (authorization_token==null) return false;
      var authorized = false;
      try {
        var token = System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(authorization_token));
        var idx = token.IndexOf(':');
        if (idx>=0) {
          var user = token.Substring(0, idx);
          var pass = token.Substring(idx+1);
          authorized = acinfo.CheckAuthorization(user, pass);
        }
      }
      catch (FormatException) {
      }
      catch (ArgumentException) {
      }
      return authorized;
    }

    public Task Invoke(IDictionary<string, object> arg)
    {
      var env = new OwinEnvironment(arg);
      if (env.TryGetValue(OwinEnvironment.PeerCastStation.AccessControlInfo, out AccessControlInfo acinfo) &&
          (acinfo.Accepts & acceptType)!=0) {
        if (acinfo.AuthorizationRequired) {
          if (CheckAuthorization(GetAuthorizationToken(env), acinfo)) {
            return nextApp.Invoke(arg);
          }
          else {
            env.Environment[OwinEnvironment.Owin.ResponseStatusCode] = 401;
            env.SetResponseHeader("WWW-Authenticate", "Basic realm=\"PeerCastStation\"");
            return Task.Delay(0);
          }
        }
        else {
          return nextApp.Invoke(arg);
        }
      }
      else {
        env.Environment[OwinEnvironment.Owin.ResponseStatusCode] = 403;
        return Task.Delay(0);
      }
    }

  }

  public static class UseAuthExtensions
  {
    public static IAppBuilder UseAuth(this IAppBuilder appBuilder, OutputStreamType acceptType)
    {
      return appBuilder.Use<AuthMiddleware>(acceptType);
    }
  }

}
