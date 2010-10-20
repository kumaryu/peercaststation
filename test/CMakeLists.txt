
include_directories(.)
include_directories(../src)

add_executable(
  test_core
  test_core_core.cpp
  test_core_socket.cpp
  src/gtest_main.cc
  gtest-all.cc
)

target_link_libraries(test_core PeCaStCore)

add_definitions(-DCURRENT_SOURCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}")

find_package(POCO REQUIRED)
include_directories(${POCO_INCLUDE_DIRS})
target_link_libraries(test_core ${POCO_LIBRARIES})

add_test(test_core test_core)
set_tests_properties(test_core PROPERTIES ENVIRONMENT CTEST_OUTPUT_ON_FAILURE=1)

