#include "stdafx.h"

using namespace System;
using namespace System::Reflection;
using namespace System::Runtime::CompilerServices;
using namespace System::Runtime::InteropServices;
using namespace System::Security::Permissions;

//
// �йس��򼯵�һ����Ϣ������
// ���ơ�������Щ����ֵ���޸�
// ����򼯹�������Ϣ��
//
[assembly:AssemblyTitleAttribute(L"FreeMote.NET")];
[assembly:AssemblyDescriptionAttribute(L"FreeMote wrapper for Emote engine")];
[assembly:AssemblyConfigurationAttribute(L"")];
[assembly:AssemblyCompanyAttribute(L"Project AZUSA")];
[assembly:AssemblyProductAttribute(L"FreeMote")];
[assembly:AssemblyCopyrightAttribute(L"Copyright (c) Project AZUSA 2017-2018")];
[assembly:AssemblyTrademarkAttribute(L"wdwxy12345@gmail.com")];
[assembly:AssemblyCultureAttribute(L"")];

//
// ���򼯵İ汾��Ϣ�������ĸ�ֵ���: 
//
//      ���汾
//      �ΰ汾
//      ���ɺ�
//      �޶���
//
// ������ָ������ֵ��Ҳ����ʹ�á��޶��š��͡����ɺš���Ĭ��ֵ��
// �����ǰ�������ʾʹ�á�*��: :

[assembly:AssemblyVersionAttribute("1.0.*")];

[assembly:ComVisible(false)];

[assembly:CLSCompliantAttribute(true)];