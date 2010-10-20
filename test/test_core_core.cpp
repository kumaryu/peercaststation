
#include "gtest/gtest.h"
#include "core/core.h"

TEST(PECACore, Initialize) 
{
  PECACore* core_module = PECACoreInitialize();
  EXPECT_TRUE(core_module);
  PECACoreTerminate(core_module);
}

TEST(PECACore, GetSetYP) 
{
  PECACore* core = PECACoreInitialize();
  EXPECT_STREQ("", PECACoreGetYP(core));
  EXPECT_EQ(7144,  PECACoreGetYPPort(core));
  char yp[] = "yp.peercast.org";
  PECACoreSetYP(core, yp, 7145);
  EXPECT_STREQ("yp.peercast.org", PECACoreGetYP(core));
  EXPECT_EQ(7145,  PECACoreGetYPPort(core));
  yp[0] = 'p';
  EXPECT_STREQ("yp.peercast.org", PECACoreGetYP(core));
  EXPECT_STREQ("pp.peercast.org", yp);
  PECACoreTerminate(core);
}

