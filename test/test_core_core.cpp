
#include "gtest/gtest.h"
#include "core/core.h"

TEST(PeCaStCore, Initialize) 
{
  PeCaStCore* core_module = PeCaStCoreInitialize();
  EXPECT_TRUE(core_module);
  PeCaStCoreTerminate(core_module);
}

