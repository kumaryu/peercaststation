
#include "gtest/gtest.h"
#include "core/socket.h"
#include "Poco/Process.h"
#include "Poco/Thread.h"
#include <iostream>

class PECASocketTest
  : public testing::Test
{
 protected:
  virtual void SetUp()
  {
    Poco::Process::Args args;
    args.push_back(CURRENT_SOURCE_DIR "/test_core_socket_serv.rb");
    mHandle = new Poco::ProcessHandle(Poco::Process::launch("ruby", args));
    Poco::Thread::sleep(100);
  }

  virtual void TearDown()
  {
    Poco::Process::kill(mHandle->id());
    delete mHandle;
  }
  Poco::ProcessHandle* mHandle;
};

TEST_F(PECASocketTest, Open) 
{
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  PECASocket* sock = PECASockOpen(SOCK_PROTO_ANY, "localhost", 1234);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  EXPECT_TRUE(sock);
  PECASockClose(sock);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
}

TEST_F(PECASocketTest, OpenRefused) 
{
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  PECASocket* sock = PECASockOpen(SOCK_PROTO_ANY, "localhost", 1235);
  EXPECT_EQ(SOCK_E_CONN_REFUSE, PECASockGetLastError());
  EXPECT_FALSE(sock);
}

TEST_F(PECASocketTest, ReadWrite) 
{
  PECASocket* sock = PECASockOpen(SOCK_PROTO_ANY, "localhost", 1234);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  EXPECT_EQ(6, PECASockWrite(sock, "Hello\n", 6));
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  char buf[256];
  EXPECT_EQ(6, PECASockRead(sock, buf, 6));
  buf[6] = 0;
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  EXPECT_STREQ("Hello\n", buf);
  PECASockClose(sock);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
}

