if(CLR_CMAKE_TARGET_TIZEN_LINUX)
  set(FEATURE_GDBJIT_LANGID_CS 1)
endif()

if(NOT DEFINED FEATURE_EVENT_TRACE)
  set(FEATURE_EVENT_TRACE 1)
endif(NOT DEFINED FEATURE_EVENT_TRACE)

if(NOT DEFINED FEATURE_PERFTRACING AND FEATURE_EVENT_TRACE)
  set(FEATURE_PERFTRACING 1)
endif(NOT DEFINED FEATURE_PERFTRACING AND FEATURE_EVENT_TRACE)

if(NOT DEFINED FEATURE_DBGIPC)
  if(CLR_CMAKE_TARGET_UNIX)
    set(FEATURE_DBGIPC 1)
  endif()
endif(NOT DEFINED FEATURE_DBGIPC)

if(NOT DEFINED FEATURE_STANDALONE_GC)
  set(FEATURE_STANDALONE_GC 1)
endif(NOT DEFINED FEATURE_STANDALONE_GC)

if(NOT DEFINED FEATURE_AUTO_TRACE)
  set(FEATURE_AUTO_TRACE 0)
endif(NOT DEFINED FEATURE_AUTO_TRACE)

if(NOT DEFINED FEATURE_SINGLE_FILE_DIAGNOSTICS)
  set(FEATURE_SINGLE_FILE_DIAGNOSTICS 1)
endif(NOT DEFINED FEATURE_SINGLE_FILE_DIAGNOSTICS)

if (CLR_CMAKE_TARGET_WIN32 OR CLR_CMAKE_TARGET_UNIX)
  set(FEATURE_COMWRAPPERS 1)
endif()

if (CLR_CMAKE_TARGET_APPLE)
  set(FEATURE_OBJCMARSHAL 1)
endif()

if (CLR_CMAKE_TARGET_WIN32)
  set(FEATURE_TYPEEQUIVALENCE 1)
endif(CLR_CMAKE_TARGET_WIN32)