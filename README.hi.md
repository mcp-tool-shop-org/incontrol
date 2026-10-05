<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.md">English</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/incontrol/readme.png" alt="InControl" width="400"></p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9-purple?style=flat-square&logo=dotnet" alt=".NET 9">
  <img src="https://img.shields.io/badge/WinUI-3-blue?style=flat-square" alt="WinUI 3">
  <a href="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml"><img src="https://github.com/mcp-tool-shop-org/incontrol/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://codecov.io/gh/mcp-tool-shop-org/incontrol"><img src="https://codecov.io/gh/mcp-tool-shop-org/incontrol/branch/main/graph/badge.svg" alt="Coverage"></a>
  <a href="https://apps.microsoft.com/detail/9N1FG39JWF83"><img src="https://img.shields.io/badge/Microsoft_Store-InControl--Desktop-0078D4?style=flat-square&logo=microsoft" alt="Microsoft Store"></a>
  <a href="https://mcp-tool-shop-org.github.io/incontrol/"><img src="https://img.shields.io/badge/docs-handbook-blue?style=flat-square" alt="Handbook"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License"></a>
</p>

**विंडोज के लिए ओलामा चैट।** इस पीसी पर डिफ़ॉल्ट रूप से। एक जीपीयू जिसे आपने एसएसएच के माध्यम से किराए पर लिया है, जब आप ऐसा करने के लिए कहेंगे।

इनकंट्रोल ओलामा एचटीटीपी एपीआई का उपयोग करता है। जब तक आप किसी को कनेक्ट नहीं करते, तब तक कुछ भी किराए पर नहीं भेजा जाता है। शीर्ष पर मौजूद बार तब उस मशीन का नाम देता है और कहता है कि चैट इस पीसी को छोड़ देगा।

## इनकंट्रोल क्यों?

- **सबसे पहले यह पीसी।** प्रॉम्प्ट तब तक यहीं रहते हैं जब तक आप किराए पर लिए गए जीपीयू को कनेक्ट नहीं करते।
- **किसी भी तरह से समान ओलामा।** एक किराए पर ली गई मशीन `127.0.0.1:11434` पर ओलामा चलाती है। इनकंट्रोल एसएसएच लोकल फॉरवर्ड के साथ इसे एक्सेस करता है। एचटीटीपी क्लाइंट कभी भी सार्वजनिक पोर्ट से बात नहीं करता है।
- **यह उपकरण अनुमति सूची नहीं है।** कनेक्टिविटी सहायक उपकरण यूआरएल को नियंत्रित करती है। यह यह तय नहीं करता है कि चैट कहाँ चलती है।
- **परियोजनाएँ और नोट्स।** फ़ाइल सत्रों को उनकी अपनी निर्देशों के साथ परियोजनाओं के अंतर्गत रखें। इसे किसी परियोजना या सत्र के लिए एक नोट याद रखने के लिए कहें, और मिलान करने वाले नोट्स अगले संदेश के साथ जुड़े रहेंगे।
- **वेब खोज, जब आप इसे चालू करते हैं।** वेब बटन एक ऐसे मॉडल को उपकरण का उपयोग करके डकडकगो खोज करने और सार्वजनिक पृष्ठों को पढ़ने देता है। जब तक आप इसे चालू नहीं करते, तब तक यह बंद रहता है, और प्रत्येक उत्तर उन खोजों को सूचीबद्ध करता है जो इसने की हैं।
- **फ़ाइलें संलग्न करें।** किसी संदेश में टेक्स्ट और कोड फ़ाइलें जोड़ें, या जेम्मा3 या लामा3.2-विज़न जैसे विज़न मॉडल के लिए छवियां जोड़ें। पेपरक्लिप, Ctrl+Shift+O का उपयोग करें, या फ़ाइलों को कंपोज़र पर छोड़ें।
- **इस पीसी पर वॉयस।** उत्तरों को कोकोरो द्वारा जोर से पढ़ा जा सकता है, जो इस पीसी पर चलता है। वॉयस मॉडल एक बार डाउनलोड होता है, पहली बार जब वॉयस की आवश्यकता होती है: जब कोई उत्तर बोला जाता है, या जब आप सेटिंग्स खोलते हैं।
- **विनयूआई 3।** एक देशी विंडोज ऐप। उत्तर मार्कडाउन प्रस्तुत करते हैं: जोर, कोड ब्लॉक, सूचियां, तालिकाएं और लिंक।
- **नीति इंजन।** संगठन, टीम और उपयोगकर्ता नीति दस्तावेज़ उपकरण, मेमोरी और कनेक्टिविटी को नियंत्रित करते हैं।
- **कनेक्टिविटी मोड।** केवल ऑफ़लाइन, सहायक, या कनेक्टेड, ऑडिट लॉग के साथ।

## नुगेट पैकेज

मुख्य लाइब्रेरी आपके स्वयं के स्थानीय एआई एकीकरण बनाने के लिए स्टैंडअलोन नुगेट पैकेजों के रूप में उपलब्ध हैं:

| पैकेज | संस्करण | विवरण |
|---------|---------|-------------|
| [InControl.Core](https://www.nuget.org/packages/InControl.Core) | [![NuGet](https://img.shields.io/nuget/v/InControl.Core?style=flat-square)](https://www.nuget.org/packages/InControl.Core) | स्थानीय एआई चैट अनुप्रयोगों के लिए डोमेन मॉडल, वार्तालाप प्रकार और साझा अमूर्तता। |
| [InControl.Inference](https://www.nuget.org/packages/InControl.Inference) | [![NuGet](https://img.shields.io/nuget/v/InControl.Inference?style=flat-square)](https://www.nuget.org/packages/InControl.Inference) | स्ट्रीमिंग चैट, मॉडल प्रबंधन और स्वास्थ्य जांच के साथ एलएलएम बैकएंड अमूर्त परत। इसमें ओलामा कार्यान्वयन शामिल है। |

```bash
dotnet add package InControl.Core
dotnet add package InControl.Inference
```

```csharp
// Example: use InControl.Inference in your own app
var client = inferenceClientFactory.GetClient();
var request = ChatRequest.Simple("llama3.2", "Hello");
await foreach (var token in client.StreamChatAsync(request))
{
    Console.Write(token);
}
```

## लक्ष्य हार्डवेयर

| घटक | न्यूनतम | अनुशंसित |
|-----------|---------|-------------|
| जीपीयू | आरटीएक्स 3060 (8 जीबी) | आरटीएक्स 4080/5080 (16 जीबी) |
| रैम | 16 जीबी | 32 जीबी |
| OS | विंडोज 10 संस्करण 2004 (x64) | विंडोज 11 |
| .नेट | 9.0 | 9.0 |

## स्थापना

**माइक्रोसॉफ्ट स्टोर:** [इनकंट्रोल-डेस्कटॉप](https://apps.microsoft.com/detail/9N1FG39JWF83)। स्टोर पैकेज अपने स्वयं के .नेट और विंडोज ऐप एसडीके रनटाइम को रखता है, और स्टोर इसे अपडेट रखता है।

**पोर्टेबल डाउनलोड:** `InControl-<version>-win-x64-portable.zip` [गिटहब रिलीज़](https://github.com/mcp-tool-shop-org/incontrol/releases/latest) पर। इसे कहीं भी अनज़िप करें और `InControl.App.exe` चलाएं; रनटाइम इसके अंदर हैं, इसलिए कुछ भी स्थापित नहीं है। यह कोड-हस्ताक्षरित नहीं है, इसलिए विंडोज आपसे पहली बार चलाने की पुष्टि करने के लिए कह सकता है। एक `.sha256` फ़ाइल इसके बगल में है।

**स्रोत से:**

```bash
git clone https://github.com/mcp-tool-shop-org/incontrol.git
cd incontrol
dotnet restore
dotnet build

# Run (Ollama on this PC)
dotnet run --project src/InControl.App
```

## आवश्यक शर्तें

इनकंट्रोल को एक स्थानीय एलएलएम बैकएंड की आवश्यकता होती है। हम [ओलामा](https://ollama.ai/) की अनुशंसा करते हैं:

```bash
# Install Ollama from https://ollama.ai/download

# Pull a model
ollama pull llama3.2

# Start the server (runs on http://127.0.0.1:11434)
ollama serve
```

## एक किराए पर लिया गया जीपीयू

सेटिंग्स → **यह चैट कहाँ चलती है।** किराए पर ली गई मशीन से सीधे एसएसएच कमांड पेस्ट करें (`ssh -p <mapped-port> root@<public-ip> -i <key>`)। बटन कहता है कि चैट उस मशीन पर भेजी जाएगी।

फॉरवर्ड इस पीसी पर `127.0.0.1:11436` पर सुनता है, न कि `11434` पर। चैट तब तक यहीं रहती है जब तक कि ओलामा उस पोर्ट के माध्यम से उत्तर नहीं देता।

किराए पर ली गई मशीन पर ओलामा को `127.0.0.1:11434` पर छोड़ दें। `OLLAMA_HOST=0.0.0.0` सेट न करें, और पोर्ट 11434 को प्रकाशित न करें। एसएसएच पोर्ट मैप किया गया sshd पोर्ट है, 11434 नहीं।

रनपॉड का `ssh.runpod.io` प्रॉक्सी केवल एक शेल है। यह पोर्ट को फॉरवर्ड नहीं कर सकता है। **मेरे रनपॉड पॉड्स को देखें** पर्यावरण से `RUNPOD_API_KEY` का उपयोग करता है और पॉड के सीधे सार्वजनिक-आईपी एसएसएच को भरता है। कुंजी संग्रहीत नहीं है। लुकअप पॉड शुरू नहीं करता है और चैट नहीं भेजता है। वैस्ट सीधे पते पर फॉरवर्ड का दस्तावेजीकरण करता है। पता तब मर जाता है जब किराए पर ली गई मशीन पुनरारंभ होती है। पॉड को फिर से देखें, या नया कमांड पेस्ट करें। निजी कुंजी इस पीसी पर रहती है।

पूर्ण नियम [docs/COMPUTE.md](docs/COMPUTE.md) में हैं।

## बिल्डिंग

### बिल्ड वातावरण सत्यापित करें

```powershell
# Run verification script
./scripts/verify.ps1
```

### विकास बिल्ड

```bash
dotnet build
```

### रिलीज़ बिल्ड

```powershell
# Creates release artifacts in artifacts/
./scripts/release.ps1
```

### परीक्षण चलाएँ

```bash
dotnet test
```

## आर्किटेक्चर

इनकंट्रोल एक स्वच्छ, स्तरित आर्किटेक्चर का पालन करता है:

```
+-------------------------------------------+
|         InControl.App (WinUI 3)           |  UI Layer
+-------------------------------------------+
|         InControl.ViewModels              |  Presentation
+-------------------------------------------+
|         InControl.Services                |  Business Logic
+-------------------------------------------+
|         InControl.Inference               |  LLM Backends
+-------------------------------------------+
|         InControl.Core                    |  Shared Types
+-------------------------------------------+
```

विस्तृत डिज़ाइन दस्तावेज़ के लिए [ARCHITECTURE.md](./docs/ARCHITECTURE.md) देखें।

### प्रमुख उप-प्रणालियाँ

| उप-प्रणाली | नेमस्पेस | उद्देश्य |
|-----------|-----------|---------|
| सहायक | `InControl.Core.Assistant` | प्रोफ़ाइल, मेमोरी स्टोर, व्यक्तित्व गार्ड, ऑनबोर्डिंग |
| प्लगइन्स | `InControl.Core.Plugins` | एसडीके के साथ मैनिफ़ेस्ट-मान्य, सैंडबॉक्स्ड विस्तारशीलता |
| नीति | `InControl.Core.Policy` | जेएसओएन नीति दस्तावेज़ (संगठन/टीम/उपयोगकर्ता), उपकरण/प्लगइन/मेमोरी/कनेक्टिविटी नियम |
| कनेक्टिविटी | `InControl.Core.Connectivity` | ऑडिट ट्रेल के साथ तीन-मोड नेटवर्क शासन |
| स्वास्थ्य | `InControl.Services.Health` | प्लग करने योग्य स्वास्थ्य जांच (ऐप, अनुमान, भंडारण) |
| निदान | `InControl.Core.Diagnostics` | स्वच्छ कॉन्फ़िगरेशन के साथ समर्थन बंडल निर्माण |

## डेटा संग्रहण

सभी डेटा स्थानीय रूप से संग्रहीत हैं:

| डेटा | स्थान |
|------|----------|
| सत्र | `%LOCALAPPDATA%\InControl\sessions\` |
| परियोजनाएँ और नोट्स | `%LOCALAPPDATA%\InControl\` |
| लॉग | `%LOCALAPPDATA%\InControl\logs\` |
| कैश (वॉयस मॉडल) | `%LOCALAPPDATA%\InControl\cache\` |
| निर्यात | `%USERPROFILE%\Documents\InControl\exports\` |

एक स्टोर इंस्टॉलेशन उस फ़ोल्डर को ऐप के अपने स्टोरेज के अंदर रख सकता है, जिसे विंडोज अनइंस्टॉल करने पर हटा देता है।

गोपनीयता नीति [PRIVACY.md](./PRIVACY.md) है। डेटा प्रबंधन विवरण [docs/PRIVACY.md](./docs/PRIVACY.md) में हैं।

## समस्या निवारण

सामान्य समस्याएं और उनके समाधान [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md) में प्रलेखित हैं।

### त्वरित समाधान

**ऐप शुरू नहीं हो रहा (स्रोत निर्माण):**
- जांचें कि .NET 9 SDK स्थापित है: `dotnet --list-sdks`
- स्टोर पैकेज को किसी अलग रनटाइम की आवश्यकता नहीं है

**कोई मॉडल उपलब्ध नहीं है:**
- सुनिश्चित करें कि ओलामा चल रहा है: `ollama serve`
- एक मॉडल डाउनलोड करें: `ollama pull llama3.2`

**प्रतिक्रिया धीमी है:**
- ओलामा यह तय करता है कि मॉडल GPU पर चलता है या नहीं। `ollama ps` दिखाता है कि इसका कितना हिस्सा GPU पर है
- GPU ड्राइवर को अपडेट करें, या एक छोटा मॉडल डाउनलोड करें

## योगदान

योगदान का स्वागत है! कृपया:

1. रिपॉजिटरी को फोर्क करें
2. एक सुविधा शाखा बनाएं
3. नई कार्यक्षमता के लिए परीक्षण लिखें
4. एक पुल अनुरोध सबमिट करें

## समस्याओं की रिपोर्ट करना

1. सबसे पहले [TROUBLESHOOTING.md](./docs/TROUBLESHOOTING.md) देखें
2. ऐप में "कॉपी डायग्नोस्टिक्स" सुविधा का उपयोग करें
3. संलग्न डायग्नोस्टिक्स जानकारी के साथ एक समस्या खोलें

## तकनीकी ढांचा

| लेयर | प्रौद्योगिकी |
|-------|------------|
| UI ढांचा | WinUI 3 (विंडोज ऐप SDK 1.6) |
| आर्किटेक्चर | CommunityToolkit.Mvvm के साथ MVVM |
| LLM एकीकरण | OllamaSharp, Microsoft.Extensions.AI |
| DI कंटेनर | Microsoft.Extensions.DependencyInjection |
| कॉन्फ़िगरेशन | Microsoft.Extensions.Configuration |
| लॉगिंग | Microsoft.Extensions.Logging + Serilog |

## संस्करण

वर्तमान संस्करण: **2.0.1**। माइक्रोसॉफ्ट स्टोर में यह InControl-Desktop है, पैकेज पहचान `mcp-tool-shop.InControl-Desktop`, संस्करण `2.0.1.0`। स्टोर में पहले से ही 1.4.0 के माध्यम से यह ऐप था, इसलिए पैकेज संस्करण इससे ऊपर से शुरू होता है। रिपॉजिटरी, विंडो और स्टार्ट टाइल पर नाम InControl है।

संस्करण क्यों बढ़ा, और 0.3.0 के इतिहास के लिए [CHANGELOG.md](./CHANGELOG.md) देखें।

## सुरक्षा और डेटा दायरा

InControl ओलामा के लिए एक WinUI 3 चैट एप्लिकेशन है।

- **एक्सेस किया गया डेटा:** इस पीसी पर ओलामा, चैट इतिहास, स्थानीय संग्रहण में परियोजनाएं और नोट्स, और, केवल एक बार जब आप इसे कनेक्ट करते हैं, तो SSH पर आप जिस मशीन तक पहुंचते हैं, उस पर ओलामा
- **एक्सेस नहीं किया गया डेटा:** कोई InControl खाता नहीं, कोई टेलीमेट्री नहीं, कोई एनालिटिक्स नहीं
- **अनुमतियाँ:** ओलामा के लिए लूपबैक HTTP, जब आप किसी किराये को कनेक्ट करते हैं तो इस पीसी पर एक SSH क्लाइंट, और चैट इतिहास के लिए फ़ाइल सिस्टम

पूर्ण नीति: [SECURITY.md](SECURITY.md)

---

## सहायता

- **बग रिपोर्ट:** [समस्याएं](https://github.com/mcp-tool-shop-org/incontrol/issues)
- **सुरक्षा:** [SECURITY.md](SECURITY.md)

## लाइसेंस

[MIT](LICENSE) -- पूर्ण पाठ के लिए [LICENSE](LICENSE) देखें।

---

<p align="center">
  Built by <a href="https://mcp-tool-shop.github.io/">MCP Tool Shop</a>
</p>
