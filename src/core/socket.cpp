
#include <string>
#include <typeinfo>
#include <Poco/Net/DNS.h>
#include <Poco/Net/StreamSocket.h>
#include <Poco/Net/IPAddress.h>
#include <Poco/Net/NetException.h>
#include <Poco/Net/TCPServer.h>
#include <Poco/ThreadLocal.h>
#include <Poco/ThreadPool.h>

#include "socket.h"
#include "stream.h"

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
  PECASocket()
    : mSocket(NULL), mOwn(true), mStream(this)
  {
  }

  PECASocket(Poco::Net::StreamSocket& socket, bool own)
    : mSocket(new Poco::Net::StreamSocket(socket)), mOwn(own), mStream(this)
  {
  }

  ~PECASocket()
  {
    Close();
  }

  PECASockError Connect(PECASockProto proto, const char* addr, unsigned short port)
  {
    try {
      Poco::Net::SocketAddress sock_addr;
      PECASockError err = Resolve(sock_addr, proto, addr, port);
      if (err==SOCK_E_NOERROR) {
        mSocket = new Poco::Net::StreamSocket(sock_addr);
        mSocket->setReceiveTimeout(Poco::Timespan(3, 0));
        mSocket->setSendTimeout(Poco::Timespan(3, 0));
        return SOCK_E_NOERROR;
      }
      else {
        return err;
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
      try {
        mSocket->shutdown();
        mSocket->close();
      }
      catch (Poco::Net::NetException&) {
        //既に閉じてたら例外が出るが気にしない
      }
    }
    if (mOwn) {
      delete mSocket;
    }
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

  static PECASockError Resolve(
      Poco::Net::SocketAddress& dest,
      PECASockProto proto,
      const char* addr,
      unsigned short port)
  {
    try {
      const Poco::Net::HostEntry& entry = Poco::Net::DNS::resolve(addr);
      const Poco::Net::HostEntry::AddressList& addrlist = entry.addresses();
      Poco::Net::IPAddress ipaddr;
      for (Poco::Net::HostEntry::AddressList::const_iterator a=addrlist.begin(); a!=addrlist.end(); a++) {
        switch (proto) {
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
        dest = Poco::Net::SocketAddress(ipaddr, port);
        return SOCK_E_NOERROR;
      }
      else {
        return SOCK_E_INTF_NOTFOUND;
      }
    }
    catch (Poco::Net::NetException& e) {
      return s_SockExceptionToError(e);
    }
    catch (Poco::IOException&) {
      return SOCK_E_NET;
    }
  }

  PECAIOStream* GetStream()
  {
    return &mStream;
  }

private:
  Poco::Net::StreamSocket* mSocket;
  bool mOwn;

  struct SocketStream
    : public PECAIOStream
  {
    SocketStream(PECASocket* sock)
      : mSocket(sock)
    {
      this->Close = s_Close;
      this->Read  = s_Read;
      this->Write = s_Write;
    }
    static void PECAAPI s_Close(PECAIOStream* s)
    {
      PECASockClose(static_cast<SocketStream*>(s)->mSocket);
    }
    static int  PECAAPI s_Read(PECAIOStream* s, void* dest, int size)
    {
      return PECASockRead(static_cast<SocketStream*>(s)->mSocket, dest, size);
    }
    static int  PECAAPI s_Write(PECAIOStream* s, const void* data, int size)
    {
      return PECASockWrite(static_cast<SocketStream*>(s)->mSocket, data, size);
    }
    PECASocket* mSocket;
  } mStream;
};

PECAIOStream* PECASockToIOStream(PECASocket* sock)
{
  return sock->GetStream();
}

PECASocket* PECAAPI PECASockOpen(PECASockProto protocol, const char* addr, unsigned short port)
{
  PECASocket* sock = new PECASocket();
  PECASockError res = sock->Connect(protocol, addr, port);
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

struct PECAServerSocket
{
private:
  class PECAServerConnection
    : public Poco::Net::TCPServerConnection
  {
  public:
    PECAServerConnection(const Poco::Net::StreamSocket& socket, PECASockCallback proc, void* proc_arg)
      : TCPServerConnection(socket), mProc(proc), mProcArg(proc_arg)
    {
    }

    virtual void run()
    {
      PECASocket* sock = new PECASocket(socket(), false);
      mProc(sock, mProcArg);
      delete sock;
    }

  private:
    PECASockCallback mProc;
    void* mProcArg;
  };

  class PECAServerConnectionFactory
    : public Poco::Net::TCPServerConnectionFactory 
  {
  public:
    PECAServerConnectionFactory(PECASockCallback proc, void* proc_arg)
      : mProc(proc), mProcArg(proc_arg)
    {
    }

    virtual Poco::Net::TCPServerConnection* createConnection(
        const Poco::Net::StreamSocket& socket)
    {
      return new PECAServerConnection(socket, mProc, mProcArg);
    }

  private:
    PECASockCallback mProc;
    void* mProcArg;
  };

public:
  PECAServerSocket(unsigned int max_clients)
    : mThreadPool(2, max_clients),
      mSocket(NULL),
      mServer(NULL)
  {
  }

  ~PECAServerSocket()
  {
    Close();
  }

  PECASockError Listen(
    PECASockProto    proto,
    const char*      intf,
    unsigned short   port,
    PECASockCallback proc,
    void*            proc_arg)
  {
    if (intf) {
      Poco::Net::SocketAddress sock_addr;
      PECASockError err = PECASocket::Resolve(sock_addr, proto, intf, port);
      if (err!=SOCK_E_NOERROR) return err;
      mSocket = new Poco::Net::ServerSocket(sock_addr);
    }
    else {
      mSocket = new Poco::Net::ServerSocket(port);
    }
    mServer = new Poco::Net::TCPServer(
        new PECAServerConnectionFactory(proc, proc_arg),
        mThreadPool,
        *mSocket);
    mServer->start();
    return SOCK_E_NOERROR;
  }

  void Close()
  {
    if (mServer) {
      mServer->stop();
    }
    delete mServer;
    delete mSocket;
    mServer = NULL;
    mSocket = NULL;
  }

private:
  Poco::ThreadPool         mThreadPool;
  Poco::Net::ServerSocket* mSocket;
  Poco::Net::TCPServer*    mServer;
};

PECAServerSocket* PECAAPI PECAServerSockOpen(
    PECASockProto    proto,
    const char*      intf,
    unsigned short   port,
    unsigned int     max_clients,
    PECASockCallback proc,
    void*            proc_arg)
{
  PECAServerSocket* sock = new PECAServerSocket(max_clients);
  PECASockError err = sock->Listen(proto, intf, port, proc, proc_arg);
  if (err==SOCK_E_NOERROR) {
    PECASockSetError(SOCK_E_NOERROR);
    return sock;
  }
  else {
    PECASockSetError(err);
    delete sock;
    return NULL;
  }
}

void PECAAPI PECAServerSockClose(PECAServerSocket* sock)
{
  if (sock) {
    sock->Close();
    delete sock;
  }
  PECASockSetError(SOCK_E_NOERROR);
}

