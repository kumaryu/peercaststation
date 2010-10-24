
#include "core/stream.h"
#include "core/atom.h"
#include <Poco/ByteOrder.h>
#include <typeinfo>
#include <string>
#include <vector>

class PECAAtomName
{
public:
  PECAAtomName() {}
  PECAAtomName(const char* x)
  {
    for (size_t i=0; i<4; i++) {
      if (*x) mName[i] = *x++;
      else    mName[i] = 0;
    }
  }
  PECAAtomName(const PECAAtomName& x)
  {
    for (size_t i=0; i<4; i++) mName[i] = x.mName[i];
  }
  PECAAtomName& operator=(const PECAAtomName& x)
  {
    for (size_t i=0; i<4; i++) mName[i] = x.mName[i];
    return *this;
  }

  bool operator==(const PECAAtomName& x) const
  {
    return
      mName[0]==x.mName[0] &&
      mName[1]==x.mName[1] &&
      mName[2]==x.mName[2] &&
      mName[3]==x.mName[3];
  }
  bool operator!=(const PECAAtomName& x) const
  {
    return !(*this==x);
  }

  std::string GetName() const
  {
    return std::string(mName, 4);
  }

  const char* GetNamePtr() const
  {
    return mName;
  }

  char* GetNamePtr()
  {
    return mName;
  }


private:
  char mName[4];
};

struct PECAAtom
{
public:
  PECAAtom(const PECAAtomName& name)
    : mName(name)
  {
  }

  virtual ~PECAAtom()
  {
  }

  virtual bool Write(PECAIOStream* stream) const = 0;

  const PECAAtomName& GetName() const
  {
    return mName;
  }

private:
  PECAAtomName mName;
};

class PECAListAtom
  : public PECAAtom
{
public:
  PECAListAtom(const PECAAtomName& name)
    : PECAAtom(name), mChildren()
  {
  }

  ~PECAListAtom()
  {
    for (std::vector<PECAAtom*>::iterator child=mChildren.begin();
         child!=mChildren.end(); child++) {
      delete *child;
    }
  }

  size_t GetChildren() const
  {
    return mChildren.size();
  }

  PECAAtom* GetChild(size_t index) const
  {
    if (index<mChildren.size()) {
      return mChildren[index];
    }
    else {
      return NULL;
    }
  }

  void Add(PECAAtom* child)
  {
    mChildren.push_back(child);
  }

  virtual bool Write(PECAIOStream* stream) const
  {
    size_t sz = stream->Write(stream, GetName().GetNamePtr(), 4);
    if (sz!=4) return false;
    Poco::UInt32 len =
      Poco::ByteOrder::toLittleEndian(
          static_cast<Poco::UInt32>(0x80000000U | mChildren.size()));
    sz = stream->Write(stream, &len, sizeof(len));
    if (sz!=sizeof(len)) return false;
    for (std::vector<PECAAtom*>::const_iterator child=mChildren.begin();
         child!=mChildren.end(); child++) {
      if (!(*child)->Write(stream)) return false;
    }
    return true;
  }

private:
  std::vector<PECAAtom*> mChildren;
};

class PECADataAtom
  : public PECAAtom
{
public:
  PECADataAtom(const PECAAtomName& name, const void* data, size_t len)
    : PECAAtom(name), mData(static_cast<const char*>(data), len)
  {
  }

  ~PECADataAtom()
  {
  }

  const std::string& GetData() const
  {
    return mData;
  }

  virtual bool Write(PECAIOStream* stream) const
  {
    size_t sz = stream->Write(stream, GetName().GetNamePtr(), 4);
    if (sz!=4) return false;
    Poco::UInt32 len = Poco::ByteOrder::toLittleEndian(static_cast<Poco::UInt32>(mData.size()));
    sz = stream->Write(stream, &len, sizeof(len));
    if (sz!=sizeof(len)) return false;
    sz = stream->Write(stream, mData.data(), mData.size());
    if (sz!=mData.size()) return false;
    return true;
  }

private:
  std::string mData;
};

PECAAtom* PECAAPI PECAAtomAllocList(const char* name)
{
  if (name && std::strlen(name)<=4) {
    return new PECAListAtom(PECAAtomName(name));
  }
  else {
    return NULL;
  }
}

void PECAAPI PECAAtomFree(PECAAtom* atom)
{
  delete atom;
}

PECAAtom* PECAAPI PECAAtomAllocData(const char* name, const void* data, size_t length)
{
  if (name && std::strlen(name)<=4) {
    return new PECADataAtom(PECAAtomName(name), data, length);
  }
  else {
    return NULL;
  }
}

PECAAtom* PECAAPI PECAAtomAllocInt(const char* name, int data)
{
  Poco::Int32 v = Poco::ByteOrder::toLittleEndian(static_cast<Poco::Int32>(data));
  return PECAAtomAllocData(name, &v, sizeof(v));
}

PECAAtom* PECAAPI PECAAtomAllocShort(const char* name, short data)
{
  Poco::Int16 v = Poco::ByteOrder::toLittleEndian(static_cast<Poco::Int16>(data));
  return PECAAtomAllocData(name, &v, sizeof(v));
}

PECAAtom* PECAAPI PECAAtomAllocByte(const char* name, unsigned char data)
{
  Poco::UInt8 v = Poco::ByteOrder::toLittleEndian(static_cast<Poco::UInt8>(data));
  return PECAAtomAllocData(name, &v, sizeof(v));
}

PECAAtom* PECAAPI PECAAtomAllocString(const char* name, const char* data)
{
  return PECAAtomAllocData(name, data, std::strlen(data)+1);
}

int PECAAPI PECAAtomIsList(PECAAtom* atom)
{
  return typeid(*atom)==typeid(PECAListAtom);
}

void PECAAPI PECAAtomGetName(PECAAtom* atom, char* name)
{
  const PECAAtomName& n = atom->GetName();
  memcpy(name, n.GetNamePtr(), 4);
  name[4] = '\0';
}

size_t PECAAPI PECAAtomGetDataLength(const PECAAtom* atom)
{
  const PECADataAtom* a = dynamic_cast<const PECADataAtom*>(atom);
  if (a) {
    return a->GetData().size();
  }
  else {
    return 0;
  }
}

size_t PECAAPI PECAAtomGetData(const PECAAtom* atom, void* dest, size_t len)
{
  const PECADataAtom* a = dynamic_cast<const PECADataAtom*>(atom);
  if (a) {
    size_t sz = std::min(len, a->GetData().size());
    memcpy(dest, a->GetData().data(), sz);
    return sz;
  }
  else {
    return 0;
  }
}

size_t PECAAPI PECAAtomGetInt(const PECAAtom* atom, int* dest)
{
  const PECADataAtom* a = dynamic_cast<const PECADataAtom*>(atom);
  if (a && a->GetData().size()==sizeof(Poco::Int32)) {
    Poco::Int32 v;
    memcpy(&v, a->GetData().data(), sizeof(v));
    *dest = Poco::ByteOrder::fromLittleEndian(v);
    return sizeof(v);
  }
  else {
    return 0;
  }
}

size_t PECAAPI PECAAtomGetShort(const PECAAtom* atom, short* dest)
{
  const PECADataAtom* a = dynamic_cast<const PECADataAtom*>(atom);
  if (a && a->GetData().size()==sizeof(Poco::Int16)) {
    Poco::Int16 v;
    memcpy(&v, a->GetData().data(), sizeof(v));
    *dest = Poco::ByteOrder::fromLittleEndian(v);
    return sizeof(v);
  }
  else {
    return 0;
  }
}

size_t PECAAPI PECAAtomGetByte(const PECAAtom* atom, unsigned char* dest)
{
  const PECADataAtom* a = dynamic_cast<const PECADataAtom*>(atom);
  if (a && a->GetData().size()==sizeof(Poco::UInt8)) {
    Poco::UInt8 v;
    memcpy(&v, a->GetData().data(), sizeof(v));
    *dest = v;
    return sizeof(v);
  }
  else {
    return 0;
  }
}

size_t PECAAPI PECAAtomGetString(const PECAAtom* atom, char* dest, size_t len)
{
  size_t sz = PECAAtomGetData(atom, dest, len);
  dest[std::min(sz, len)] = '\0';
  return sz;
}

size_t PECAAtomGetChildren(PECAAtom* atom)
{
  const PECAListAtom* a = dynamic_cast<const PECAListAtom*>(atom);
  if (a) {
    return a->GetChildren();
  }
  else {
    return 0;
  }
}

PECAAtom* PECAAPI PECAAtomGetChild(PECAAtom* atom, size_t index)
{
  const PECAListAtom* a = dynamic_cast<const PECAListAtom*>(atom);
  if (a) {
    return a->GetChild(index);
  }
  else {
    return NULL;
  }
}

void PECAAPI PECAAtomAddChild(PECAAtom* atom, PECAAtom* child)
{
  PECAListAtom* a = dynamic_cast<PECAListAtom*>(atom);
  if (a) {
    a->Add(child);
  }
}

int PECAAPI PECAAtomWrite(PECAAtom* atom, PECAIOStream* stream)
{
  return atom->Write(stream);
}

PECAAtom* PECAAPI PECAAtomRead(PECAIOStream* stream)
{
  PECAAtomName name;
  size_t sz = stream->Read(stream, name.GetNamePtr(), 4);
  if (sz!=4) return NULL;

  Poco::UInt32 len;
  sz = stream->Read(stream, &len, sizeof(len));
  if (sz!=sizeof(len)) return NULL;
  len = Poco::ByteOrder::fromLittleEndian(len);

  if (len & 0x80000000U) {
    PECAListAtom* res = new PECAListAtom(name);
    len = len & 0x7FFFFFFFU;
    for (unsigned int i=0; i<len; i++) {
      PECAAtom* child = PECAAtomRead(stream);
      if (!child) {
        delete res;
        return NULL;
      }
      res->Add(child);
    }
    return res;
  }
  else {
    unsigned char* data = new unsigned char[len];
    sz = stream->Read(stream, data, len);
    if (sz!=len) {
      delete[] data;
      return NULL;
    }
    PECAAtom* res = new PECADataAtom(name, data, len);
    delete[] data;
    return res;
  }
}


