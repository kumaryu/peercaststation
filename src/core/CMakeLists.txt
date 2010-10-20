
add_library(PeCaStCore SHARED core.cpp socket.cpp)

find_package(Threads REQUIRED)
find_package(POCO REQUIRED)
include_directories(${POCO_INCLUDE_DIRS} ${POCO_Net_INCLUDE_DIRS})
target_link_libraries(PeCaStCore ${POCO_LIBRARIES} ${POCO_Net_LIBRARIES})

