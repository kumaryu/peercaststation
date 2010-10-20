
#include "gtest/gtest.h"
#include "core/socket.h"
#include "Poco/Process.h"
#include "Poco/Thread.h"
#include <iostream>

class PeCaStSocketTest
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

TEST_F(PeCaStSocketTest, Open) 
{
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
  PeCaStSocket* sock = PeCaStSockOpen(SOCK_PROTO_ANY, "localhost", 1234);
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
  EXPECT_TRUE(sock);
  PeCaStSockClose(sock);
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
}

TEST_F(PeCaStSocketTest, OpenRefused) 
{
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
  PeCaStSocket* sock = PeCaStSockOpen(SOCK_PROTO_ANY, "localhost", 1235);
  EXPECT_EQ(SOCK_E_CONN_REFUSE, PeCaStSockGetLastError());
  EXPECT_FALSE(sock);
}

TEST_F(PeCaStSocketTest, ReadWrite) 
{
  PeCaStSocket* sock = PeCaStSockOpen(SOCK_PROTO_ANY, "localhost", 1234);
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
  EXPECT_EQ(6, PeCaStSockWrite(sock, "Hello\n", 6));
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
  char buf[256];
  EXPECT_EQ(6, PeCaStSockRead(sock, buf, 6));
  buf[6] = 0;
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
  EXPECT_STREQ("Hello\n", buf);
  PeCaStSockClose(sock);
  EXPECT_EQ(SOCK_E_NOERROR, PeCaStSockGetLastError());
}

