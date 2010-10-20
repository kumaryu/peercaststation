
#include "core.h"
#include <string>

struct PECACore
{
public:
  PECACore() : mYP(), mYPPort(7144) {}

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

PECACore* PECAAPI PECACoreInitialize()
{
  return new PECACore();
}

void PECAAPI PECACoreTerminate(PECACore* core)
{
  delete core;
}

void PECAAPI PECACoreSetYP(
    PECACore* core, const char* addr, short port)
{
  core->SetYP(addr, port);
}

const char* PECAAPI PECACoreGetYP(PECACore* core)
{
  return core->GetYPAddress().c_str();
}

short PECAAPI PECACoreGetYPPort(PECACore* core)
{
  return core->GetYPPort();
}



