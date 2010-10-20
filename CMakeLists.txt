cmake_minimum_required(VERSION 2.8)
project(PeerCastStation)

set(CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake" ${CMAKE_MODULE_PATH})
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall")
set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wall")

ENABLE_TESTING()
add_subdirectory(src)
add_subdirectory(test)

