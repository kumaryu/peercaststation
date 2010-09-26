
#include "core.h"

struct PeCaStCore
{
};

PeCaStCore* PECAAPI PeCaStCoreInitialize()
{
  return new PeCaStCore();
}

void PECAAPI PeCaStCoreTerminate(PeCaStCore* core)
{
  delete core;
}

