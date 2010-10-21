
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
    Poco::Thread::sleep(300);
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
  PECASocket* sock = PECASockOpen(SOCK_PROTO_ANY, "localhost", 1234);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  EXPECT_TRUE(sock);
  PECASockClose(sock);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
}

TEST_F(PECASocketTest, OpenRefused) 
{
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

TEST(PECAServerSocket, Open) 
{
  int test_arg = 7144;
  struct OpenTestProc {
    static void PECAAPI Proc(PECASocket* sock, void* arg)
    {
      EXPECT_TRUE(sock);
      EXPECT_EQ(7144, *static_cast<int*>(arg));
    }
  };
  PECAServerSocket* sock = PECAServerSockOpen(
      SOCK_PROTO_ANY, NULL, 2234,
      4,
      OpenTestProc::Proc, &test_arg);
  EXPECT_TRUE(sock);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  PECAServerSockClose(sock);
}

TEST(PECAServerSocket, OpenAndConnect) 
{
  struct ReadWriteTestProc {
    static void PECAAPI Proc(PECASocket* sock, void*)
    {
      EXPECT_NO_THROW({
        char buf[256];
        int len = PECASockRead(sock, buf, 6);
        EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
        EXPECT_EQ(6, len);
        buf[len] = 0;
        EXPECT_STREQ("Hello\n", buf);
        len = PECASockWrite(sock, buf, 6);
        EXPECT_EQ(6, len);
        EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());

        len = PECASockRead(sock, buf, 6);
        EXPECT_EQ(0, len);
      });
    }
  };
  PECAServerSocket* sock = PECAServerSockOpen(
      SOCK_PROTO_ANY, "localhost", 2234,
      4,
      ReadWriteTestProc::Proc, NULL);
  EXPECT_TRUE(sock);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  Poco::Thread::sleep(100);

  PECASocket* client = PECASockOpen(SOCK_PROTO_ANY, "localhost", 2234);
  int len = PECASockWrite(client, "Hello\n", 6);
  EXPECT_EQ(6, len);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  char buf[256];
  len = PECASockRead(client, buf, 6);
  EXPECT_EQ(6, len);
  buf[len] = 0;
  EXPECT_STREQ("Hello\n", buf);
  EXPECT_EQ(SOCK_E_NOERROR, PECASockGetLastError());
  PECASockClose(client);

  PECAServerSockClose(sock);
}

