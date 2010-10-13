

include_directories(.)
include_directories(../src)

add_executable(test_core test_core_core.cpp src/gtest_main.cc gtest-all.cc)
target_link_libraries(test_core PeCaStCore)

add_test(test_core test_core)
set_tests_properties(test_core PROPERTIES ENVIRONMENT CTEST_OUTPUT_ON_FAILURE=1)

