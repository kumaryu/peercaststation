
add_library(PECACore SHARED
  core.cpp
  socket.cpp
  atom.cpp
)

find_package(Threads REQUIRED)
find_package(POCO REQUIRED)
include_directories(${POCO_INCLUDE_DIRS} ${POCO_Net_INCLUDE_DIRS})
target_link_libraries(PECACore ${POCO_LIBRARIES} ${POCO_Net_LIBRARIES})

