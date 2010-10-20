
#include <string>
#include <typeinfo>
#include <Poco/Net/DNS.h>
#include <Poco/Net/StreamSocket.h>
#include <Poco/Net/IPAddress.h>
#include <Poco/Net/NetException.h>
#include <Poco/ThreadLocal.h>

#include "socket.h"

static Poco::ThreadLocal<PECASockError> s_SockError;
static inline void PECASockSetError(PECASockError err)
{
  s_SockError.get() = err;
}

static inline PECASockError s_SockExceptionToError(const Poco::Net::NetException& e)
{
  if (typeid(e)==typeid(const Poco::Net::ConnectionAbortedException&)) {
    return SOCK_E_CONN_ABORT;
  }
  else if (typeid(e)==typeid(const Poco::Net::ConnectionResetException&)) {
    return SOCK_E_CONN_RESET;
  }
  else if (typeid(e)==typeid(const Poco::Net::ConnectionRefusedException&)) {
    return SOCK_E_CONN_REFUSE;
  }
  else if (typeid(e)==typeid(const Poco::Net::HostNotFoundException&)) {
    return SOCK_E_HOST_NOTFOUND;
  }
  else if (typeid(e)==typeid(const Poco::Net::NoAddressFoundException&)) {
    return SOCK_E_NO_ADDRESS;
  }
  else if (typeid(e)==typeid(const Poco::Net::InterfaceNotFoundException&)) {
    return SOCK_E_INTF_NOTFOUND;
  }
  else if (typeid(e)==typeid(const Poco::Net::InvalidAddressException&)) {
    return SOCK_E_ADDRESS;
  }
  else if (typeid(e)==typeid(const Poco::Net::ServiceNotFoundException&)) {
    return SOCK_E_SERVICE_NOTFOUND;
  }
  else if (typeid(e)==typeid(const Poco::Net::DNSException&)) {
    return SOCK_E_DNS;
  }
  else {
    return SOCK_E_NET;
  }
}

PECASockError PECAAPI PECASockGetLastError()
{
  return s_SockError.get();
}

struct PECASocket
{
public:
  PECASocket(PECASockProto protocol)
    : mProto(protocol),
      mSocket(NULL)
  {
  }

  ~PECASocket()
  {
    Close();
  }

  PECASockError Connect(const char* addr, unsigned short port)
  {
    try {
      const Poco::Net::HostEntry& entry = Poco::Net::DNS::resolve(addr);
      const Poco::Net::HostEntry::AddressList& addrlist = entry.addresses();
      Poco::Net::IPAddress ipaddr;
      for (Poco::Net::HostEntry::AddressList::const_iterator a=addrlist.begin(); a!=addrlist.end(); a++) {
        switch (mProto) {
        case SOCK_PROTO_ANY:
          ipaddr = *a;
          break;
        case SOCK_PROTO_INET:
          if (a->af()==AF_INET) ipaddr = *a;
          break;
        case SOCK_PROTO_INET6:
          if (a->af()==AF_INET6) ipaddr = *a;
          break;
        }
        if (!ipaddr.isWildcard()) break;
      }
      if (!ipaddr.isWildcard()) {
        mSocket = new Poco::Net::StreamSocket(Poco::Net::SocketAddress(ipaddr, port));
        mSocket->setReceiveTimeout(Poco::Timespan(3, 0));
        mSocket->setSendTimeout(Poco::Timespan(3, 0));
        return SOCK_E_NOERROR;
      }
      else {
        return SOCK_E_HOST_NOTFOUND;
      }
    }
    catch (Poco::Net::NetException& e) {
      return s_SockExceptionToError(e);
    }
    catch (Poco::IOException&) {
      return SOCK_E_NET;
    }
  }

  void Close()
  {
    if (mSocket) {
      mSocket->shutdown();
      mSocket->close();
    }
    delete mSocket;
    mSocket = NULL;
  }

  int Read(void* dest, int size)
  {
    if (mSocket) {
      try {
        return mSocket->receiveBytes(dest, size);
      }
      catch (Poco::TimeoutException&) {
        PECASockSetError(SOCK_E_TIMEOUT);
        return -1;
      }
      catch (Poco::Net::NetException& e) {
        PECASockSetError(s_SockExceptionToError(e));
        return -1;
      }
    }
    else {
      return -1;
    }
  }

  int Write(const void* data, int size)
  {
    if (mSocket) {
      try {
        return mSocket->sendBytes(data, size);
      }
      catch (Poco::TimeoutException&) {
        PECASockSetError(SOCK_E_TIMEOUT);
        return -1;
      }
      catch (Poco::Net::NetException& e) {
        PECASockSetError(s_SockExceptionToError(e));
        return -1;
      }
    }
    else {
      return -1;
    }
  }

private:
  PECASockProto mProto;
  Poco::Net::StreamSocket* mSocket;
};

PECASocket* PECAAPI PECASockOpen(PECASockProto protocol, const char* addr, unsigned short port)
{
  PECASocket* sock = new PECASocket(protocol);
  PECASockError res = sock->Connect(addr, port);
  PECASockSetError(res);
  if (res==SOCK_E_NOERROR) {
    return sock;
  }
  else {
    delete sock;
    return NULL;
  }
}

void PECAAPI PECASockClose(PECASocket* sock)
{
  delete sock;
}

int PECAAPI PECASockRead(PECASocket* sock, void* dest, int size)
{
  if (sock) {
    int res = sock->Read(dest, size);
    if (res>=0) PECASockSetError(SOCK_E_NOERROR);
    return res;
  }
  else {
    return -1;
  }
}

int PECAAPI PECASockWrite(PECASocket* sock, const void* data, int size)
{
  if (sock) {
    int res = sock->Write(data, size);
    if (res>=0) PECASockSetError(SOCK_E_NOERROR);
    return res;
  }
  else {
    return -1;
  }
}

