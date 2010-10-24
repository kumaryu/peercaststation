
#include "gtest/gtest.h"
#include "core/atom.h"
#include "core/stream.h"

TEST(PECAAtom, Alloc) 
{
  PECAAtom* atom = PECAAtomAllocList(NULL);
  EXPECT_FALSE(atom);

  atom = PECAAtomAllocList("foobar");
  EXPECT_FALSE(atom);

  atom = PECAAtomAllocList("atom");
  EXPECT_TRUE(atom);
  PECAAtomFree(atom);

  atom = PECAAtomAllocList("foo");
  EXPECT_TRUE(atom);
  PECAAtomFree(atom);

  atom = PECAAtomAllocData(NULL, NULL, 0);
  EXPECT_FALSE(atom);

  atom = PECAAtomAllocData("foobar", NULL, 0);
  EXPECT_FALSE(atom);

  atom = PECAAtomAllocData("atom", NULL, 0);
  EXPECT_TRUE(atom);
  PECAAtomFree(atom);

  atom = PECAAtomAllocData("foo", NULL, 0);
  EXPECT_TRUE(atom);
  PECAAtomFree(atom);
}

TEST(PECAAtom, GetName) 
{
  char name[8];
  memset(name, '?', sizeof(name));
  PECAAtom* atom = PECAAtomAllocList("atom");
  PECAAtomGetName(atom, name);
  EXPECT_STREQ("atom", name);
  PECAAtomFree(atom);

  memset(name, '?', sizeof(name));
  atom = PECAAtomAllocData("foo", NULL, 0);
  PECAAtomGetName(atom, name);
  EXPECT_STREQ("foo", name);
  PECAAtomFree(atom);
}

TEST(PECAAtom, IsList) 
{
  PECAAtom* atom = PECAAtomAllocList("atom");
  EXPECT_TRUE(PECAAtomIsList(atom));
  PECAAtomFree(atom);

  atom = PECAAtomAllocData("atom", NULL, 0);
  EXPECT_FALSE(PECAAtomIsList(atom));
  PECAAtomFree(atom);
}

TEST(PECAAtom, GetData) 
{
  {
    PECAAtom* atom = PECAAtomAllocInt("foo",  4);
    int value;
    EXPECT_EQ(4U, PECAAtomGetDataLength(atom));
    EXPECT_TRUE(PECAAtomGetInt(atom, &value));
    EXPECT_EQ(4, value);
    PECAAtomFree(atom);
  }
  {
    PECAAtom* atom = PECAAtomAllocShort("hoge", 10);
    short value;
    EXPECT_EQ(2U, PECAAtomGetDataLength(atom));
    EXPECT_TRUE(PECAAtomGetShort(atom, &value));
    EXPECT_EQ(10, value);
    PECAAtomFree(atom);
  }
  {
    PECAAtom* atom = PECAAtomAllocByte("bar", 9);
    unsigned char value;
    EXPECT_EQ(1U, PECAAtomGetDataLength(atom));
    EXPECT_TRUE(PECAAtomGetByte(atom, &value));
    EXPECT_EQ(9U, value);
    PECAAtomFree(atom);
  }
  {
    PECAAtom* atom = PECAAtomAllocString("foo", "peca");
    char value[256];
    EXPECT_EQ(5U, PECAAtomGetDataLength(atom));
    EXPECT_TRUE(PECAAtomGetString(atom, value, sizeof(value)));
    EXPECT_STREQ("peca", value);
    PECAAtomFree(atom);
  }
  {
    PECAAtom* atom = PECAAtomAllocData("bar", "pecapeca", 6);
    unsigned char value[256];
    EXPECT_EQ(6U, PECAAtomGetDataLength(atom));
    EXPECT_EQ(6U, PECAAtomGetData(atom, value, sizeof(value)));
    for (unsigned int i=0; i<6; i++) {
      EXPECT_EQ(static_cast<unsigned char>("pecapeca"[i]), value[i]);
    }
    PECAAtomFree(atom);
  }
  {
    PECAAtom* atom = PECAAtomAllocList("atom");
    EXPECT_EQ(0U, PECAAtomGetChildren(atom));
    PECAAtom* sub = PECAAtomAllocInt("sub", 100);
    PECAAtomAddChild(atom, sub);
    EXPECT_EQ(1U, PECAAtomGetChildren(atom));
    EXPECT_EQ(sub, PECAAtomGetChild(atom, 0));
    PECAAtomFree(atom);
  }
}

#include <sstream>
struct PECAMemStream
  : public PECAIOStream 
{
  PECAMemStream()
  {
    Close = s_Close;
    Read  = s_Read;
    Write = s_Write;
  }
  std::stringstream stream;

  static void PECAAPI s_Close(PECAIOStream* s) {}
  static int PECAAPI s_Read(PECAIOStream* s, void* dest, int size)
  {
    return static_cast<PECAMemStream*>(s)->stream.readsome(static_cast<char*>(dest), size);
  }

  static int PECAAPI s_Write(PECAIOStream* s, const void* data, int size)
  {
    static_cast<PECAMemStream*>(s)->stream.write(static_cast<const char*>(data), size);
    return size;
  }
};

TEST(PECAAtom, WriteReadDataAtom) 
{
  PECAMemStream* stream = new PECAMemStream();
  PECAAtom* atom = PECAAtomAllocInt("foo", 4);

  EXPECT_TRUE(PECAAtomWrite(atom, stream));
  EXPECT_EQ(4U+4U+4U, stream->stream.str().size());
  PECAAtomFree(atom);

  stream->stream.seekg(0);
  atom = PECAAtomRead(stream);
  EXPECT_FALSE(PECAAtomIsList(atom));
  char name[5];
  PECAAtomGetName(atom, name);
  EXPECT_STREQ("foo", name);
  int value;
  EXPECT_EQ(4U, PECAAtomGetDataLength(atom));
  EXPECT_TRUE(PECAAtomGetInt(atom, &value));
  EXPECT_EQ(4, value);
  PECAAtomFree(atom);
}

TEST(PECAAtom, WriteReadListAtom) 
{
  PECAMemStream* stream = new PECAMemStream();
  PECAAtom* atom = PECAAtomAllocList("list");
  PECAAtomAddChild(atom, PECAAtomAllocInt("sub1", 3190));
  PECAAtomAddChild(atom, PECAAtomAllocShort("sub2", 22222));
  PECAAtomAddChild(atom, PECAAtomAllocString("sub3", "pecapeca"));
  EXPECT_EQ(3U, PECAAtomGetChildren(atom));
  EXPECT_TRUE(PECAAtomWrite(atom, stream));
  EXPECT_EQ(8U*4U+4U+2U+9U, stream->stream.str().size());
  PECAAtomFree(atom);

  stream->stream.seekg(0);
  atom = PECAAtomRead(stream);
  EXPECT_TRUE(PECAAtomIsList(atom));
  char name[5];
  PECAAtomGetName(atom, name);
  EXPECT_STREQ("list", name);
  EXPECT_EQ(3U, PECAAtomGetChildren(atom));
  {
    PECAAtom* sub = PECAAtomGetChild(atom, 0);
    EXPECT_TRUE(sub);
    PECAAtomGetName(sub, name);
    EXPECT_STREQ("sub1", name);
    EXPECT_EQ(4U, PECAAtomGetDataLength(sub));
    int value;
    EXPECT_TRUE(PECAAtomGetInt(sub, &value));
    EXPECT_EQ(3190, value);
  }
  {
    PECAAtom* sub = PECAAtomGetChild(atom, 1);
    EXPECT_TRUE(sub);
    PECAAtomGetName(sub, name);
    EXPECT_STREQ("sub2", name);
    EXPECT_EQ(2U, PECAAtomGetDataLength(sub));
    short value;
    EXPECT_TRUE(PECAAtomGetShort(sub, &value));
    EXPECT_EQ(22222, value);
  }
  {
    PECAAtom* sub = PECAAtomGetChild(atom, 2);
    EXPECT_TRUE(sub);
    PECAAtomGetName(sub, name);
    EXPECT_STREQ("sub3", name);
    EXPECT_EQ(9U, PECAAtomGetDataLength(sub));
    char value[PECAAtomGetDataLength(sub)];
    EXPECT_TRUE(PECAAtomGetString(sub, value, sizeof(value)));
    EXPECT_STREQ("pecapeca", value);
  }
  PECAAtomFree(atom);
}


