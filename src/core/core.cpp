
#include "core.h"
#include <string>

struct PeCaStCore
{
public:
  PeCaStCore() : mYP(), mYPPort(7144) {}

  void SetYP(const std::string& addr, short port)
  {
    mYP = addr;
    mYPPort = port;
  }

  const std::string& GetYPAddress() const
  {
    return mYP;
  }

  short GetYPPort() const
  {
    return mYPPort;
  }

private:
  std::string mYP;
  short       mYPPort;
};

PeCaStCore* PECAAPI PeCaStCoreInitialize()
{
  return new PeCaStCore();
}

void PECAAPI PeCaStCoreTerminate(PeCaStCore* core)
{
  delete core;
}

void PECAAPI PeCaStCoreSetYP(
    PeCaStCore* core, const char* addr, short port)
{
  core->SetYP(addr, port);
}

const char* PECAAPI PeCaStCoreGetYP(PeCaStCore* core)
{
  return core->GetYPAddress().c_str();
}

short PECAAPI PeCaStCoreGetYPPort(PeCaStCore* core)
{
  return core->GetYPPort();
}



