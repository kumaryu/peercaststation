
#include "gtest/gtest.h"
#include "core/core.h"

TEST(PeCaStCore, Initialize) 
{
  PeCaStCore* core_module = PeCaStCoreInitialize();
  EXPECT_TRUE(core_module);
  PeCaStCoreTerminate(core_module);
}

TEST(PeCaStCore, GetSetYP) 
{
  PeCaStCore* core = PeCaStCoreInitialize();
  EXPECT_STREQ("", PeCaStCoreGetYP(core));
  EXPECT_EQ(7144,  PeCaStCoreGetYPPort(core));
  char yp[] = "yp.peercast.org";
  PeCaStCoreSetYP(core, yp, 7145);
  EXPECT_STREQ("yp.peercast.org", PeCaStCoreGetYP(core));
  EXPECT_EQ(7145,  PeCaStCoreGetYPPort(core));
  yp[0] = 'p';
  EXPECT_STREQ("yp.peercast.org", PeCaStCoreGetYP(core));
  EXPECT_STREQ("pp.peercast.org", yp);
  PeCaStCoreTerminate(core);
}

