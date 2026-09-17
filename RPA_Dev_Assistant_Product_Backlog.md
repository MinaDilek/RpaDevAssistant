# RPA Dev Assistant – Product Backlog & Implementation Checklist

> Bu belge, RPA Dev Assistant projesinde planlanan özellikleri takip etmek için hazırlanmıştır.
> Codex bu dosyayı repository içinde okuyabilir, mevcut implementasyonu inceleyebilir ve gerçekten tamamlanmış maddeleri `[x]` olarak işaretleyebilir.

## Codex için kullanım talimatı

Bu dosyayı proje backlog/checklist kaynağı olarak kullan.

Kurallar:

- Repository içindeki mevcut kodu, testleri, frontend/backend yapısını ve dokümantasyonu incele.
- Bir maddeyi yalnızca gerçekten implement edilmiş ve çalışır durumda olduğuna dair kod/test/entegrasyon kanıtı varsa `[x]` yap.
- Kısmen yapılmış maddeleri `[ ]` bırak ve sonuna `— Kısmi: ...` şeklinde kısa not ekle.
- Sadece dosya veya sınıf adı var diye bir özelliği tamamlanmış sayma.
- Mock/sample seviyesindeki özellikleri production-ready olarak işaretleme.
- Mevcut özellikleri yeniden implement etme.
- Her inceleme sonunda:
  - Tamamlanan maddeleri işaretle.
  - Kısmi maddelere kısa durum notu ekle.
  - Yeni tespit edilen teknik borç veya eksik özellikleri en alttaki `Yeni Tespitler` bölümüne ekle.
- Bu belgeyi güncellerken başlık yapısını koru.

---

## 1. Analyzer & Code Quality

- [x] Gerçek UiPath projelerinde validation ve false-positive audit
- [x] Rule set’in genişletilmesi
- [x] UiPath Windows / Windows-Legacy / Modern / Classic proje desteğinin sağlamlaştırılması — Merkezi runtime/design compatibility matrisi, analiz context'i, RPA016 davranışı, Rule Catalog metadata'sı ve odaklı testler tamamlandı.
- [x] Workflow complexity analizi
- [x] Nesting depth analizi
- [x] Büyük workflow tespiti
- [x] Hard-coded değer tespiti
- [x] Hard-coded URL / dosya yolu tespiti — RPA020 (dosya yolu) ve RPA035 (URL) kuralları tamamlandı.
- [x] Credential / secret güvenlik kontrolleri
- [x] Selector kalite analizi
- [x] `idx` ve kırılgan selector kontrolleri
- [x] Timeout / ContinueOnError kontrolleri
- [x] Retry Scope / Check App State kullanım analizi — RPA046, aynı workflow içinde en az iki uzun literal Delay ve UI activity bulunup Retry Scope/Check App State bulunmadığında tek workflow-level Suggestion üretir; sayaçları bulgu evidence'ında gösterir.
- [x] Exception handling kalite analizi
- [x] BusinessRuleException kullanım analizi
- [x] Logging kalite analizi
- [x] Argument naming convention kontrolleri
- [x] Variable naming convention kontrolleri
- [x] Kullanılmayan argument/variable tespiti — RPA033 stable scope üzerinden nested variable shadowing'i; RPA034 workflow içi referansları, static caller mapping'lerini ve runtime `ArgumentsVariable` sözleşmelerini fail-safe biçimde değerlendirir.
- [x] Invoke Workflow argument mapping kontrolleri — RPA043 bilinmeyen mapping key'lerini, RPA045 caller/callee direction uyuşmazlıklarını ve RPA044 serialized default değeri olmayan eksik In/InOut mapping'lerini statik çağrılarda doğrular; runtime dictionary ve dynamic invoke çağrıları fail-safe biçimde dışarıda bırakılır.
- [x] Skor sisteminin gerçek projelere göre kalibre edilmesi

## 2. Workflow Intelligence

- [x] Workflow detay ekranı
- [x] Activity hierarchy/tree görünümü
- [x] Workflow bazlı Findings ekranı
- [x] Workflow bazlı AI Review
- [x] Workflow invocation graph
- [x] Süreç ve PDD Analizi — Mevcut proje analizini yeniden parse etmeden kullanan ayrı TR/EN ekran; süreç özeti, sistemler, akış, kanıta dayalı iş kuralları, salt-okunur TXT/Markdown PDD karşılaştırması ve gap analizi destekleniyor.
- [ ] Project Architecture görselleştirmesi
- [x] Caller / Callee analizi
- [x] Kullanılmayan workflow tespiti — RPA037 kuralı ile entry point erişilebilirlik analizi tamamlandı.
- [x] Circular workflow reference tespiti — RPA036 kuralı ile invocation graph DFS döngü tespiti tamamlandı.
- [x] REFramework detaylı uygunluk analizi — Canonical workflow kapsamı, root Main, Main State Machine, core workflow parse sağlığı, static graph erişilebilirliği, bozuk Invoke Workflow referansları ve Config workbook varlığı açıklanabilir Pass/Fail/Unknown checklist'iyle analiz edilir; dynamic invoke ilişkileri yanlış fail üretmez.
- [ ] Queue kullanım analizi
- [x] Config kullanım analizi
- [x] Config-kod karşılaştırması ile Config’de tanımlı ancak kodda kullanılmayan key/değerlerin tespiti
- [x] Kullanılmayan Config key/değerlerini temizleyerek yeni Config dosyası üretme
- [x] Kodda referans verilen ancak Config dosyasında bulunmayan Config key’lerinin tespiti
- [x] Kod içindeki hard-coded değerlerin Config’e taşınmaya uygunluk analizi — dinamik selector değerleri hariç
- [x] Hard-coded URL, dosya/klasör yolu, e-posta, Queue adı, Asset adı, timeout, retry count, environment ve API endpoint değerlerinin Config adayı olarak sınıflandırılması
- [x] Genel/sabit teknik değerlerin (`OK`, `True`, `1` vb.) yanlış pozitif üretmemesi için Safe Extraction Candidate filtresi
- [x] Config’e taşınacak hard-coded değerler için anlamlı Config key adı önerileri
- [x] Birden fazla workflow’da tekrar eden aynı hard-coded değerlerin tespiti ve Config’e taşıma önerisi
- [x] Config key kullanım/etki analizi: hangi workflow’larda ve kaç noktada kullanıldığının gösterilmesi
- [x] Config Usage Graph: Config key → kullanan workflow/activity ilişkilerinin görselleştirilmesi
- [x] Proje dışında bulunan `.xlsx` Config workbook'unun native dosya seçiciyle seçilip Analyze / Preview / Generate akışlarında kullanılabilmesi
- [x] Config değişiklikleri uygulanmadan önce Remove / Add / Change önizlemesi
- [x] Orijinal Config dosyasını değiştirmeden düzenlenmiş yeni Config dosyası üretme
- [x] Yeni Config dosyası için kullanıcı tarafından seçilebilir `Save As` indirme/kayıt yolu
- [x] Config dosyası düzenlenirken mevcut sheet/kolon yapısı ve mümkün olduğunca formatın korunması — Workbook kopyası üzerinde satır yapısı ve stiller korunuyor; eklenen satırlarda table, auto-filter, data validation ve named range kapsamları güvenli biçimde genişletiliyor. Merged cells, formüller, kolon ayarları ve workbook metadata odaklı regression testleriyle doğrulandı.
- [ ] DEV / TEST / PROD gibi environment bazlı Config değerlerinin analiz edilmesi
- [x] Password, token, API key gibi hassas hard-coded değerler için Config yerine Orchestrator Asset / Credential önerisi
- [x] Flowchart tabanlı workflow’ları Sequence/standart workflow yapısına dönüştürme — Safe olarak sınıflanan root Flowchart workflow için controlled apply + backup + rollback; standalone XAML akışında ise orijinali değiştirmeden yeni Sequence dosyası olarak kaydetme destekleniyor.
- [x] Flowchart → Workflow dönüşüm önizlemesi — Workflow detail ve bağımsız Flowchart Converter sayfasında destekleniyor.
- [x] Flowchart dönüşümünde branch/decision mantığının korunması — FlowDecision, FlowSwitch ve shared continuation davranışı regression testleriyle korunuyor.
- [x] Flowchart içinde custom dependency aktivitelerinin tespiti, standart UiPath muadillerinin önerilmesi ve kullanıcı onayıyla XAML üzerinde otomatik değiştirilmesi — Analiz aşamasında custom namespace/aktivite tespiti, UI üzerinde standart UiPath aktivite öneri kartı/tablosu ve onaylı kaydetmede Sequence XAML içinde `ui:` namespace/aktivite dönüşümü tamamlandı.

## 3. Dependency & Package Analysis

- [x] Dependency/package analizi
- [x] Eski UiPath package tespiti — Offline legacy indicator korunuyor; UiPath resmî NuGet V3 metadata üzerinden deprecated version ve alternatif package bilgisi timeout/cache/fail-safe davranışıyla doğrulanıyor.
- [x] Uyumsuz package versiyonlarının tespiti
- [x] Kullanılmayan dependency tespiti
- [x] Modern / Classic activity uyumsuzluk analizi
- [x] Package version risk analizi — Declared-version alignment ile birlikte resmî UiPath/NuGet V3 metadata üzerinden latest stable version, deprecation ve vulnerability riskleri analiz ediliyor; metadata erişilemezse sonuç `Unknown` kalıyor.

## 4. AI Features

- [x] AI Project Review kalitesinin geliştirilmesi — Deterministic eval fixture'ları, structured response validation, güvenli fallback ve kanıta bağlı response mapping eklendi.
- [x] Workflow bazlı AI Review’un geliştirilmesi
- [x] Ask Project yeteneklerinin genişletilmesi
- [x] Evidence/citation sisteminin geliştirilmesi
- [x] AI cevaplarının güven skorunun geliştirilmesi
- [x] AI cevaplarında evidence ve interpretation ayrımının iyileştirilmesi — AI Review ve Ask Project contract'larında trusted evidence ile interpretation additive ve geriye uyumlu biçimde ayrıldı.
- [x] Türkçe AI Review desteği
- [x] Türkçe Ask Project desteğinin geliştirilmesi
- [ ] Local LLM provider desteği
- [ ] AI provider seçimi

## 5. Fix & Refactoring

- [x] Fix Suggestion doğruluğunun artırılması
- [x] Patch Preview geliştirmeleri
- [x] Güvenli Apply Fix
- [ ] Apply All Fix
- [x] Fix öncesi backup
- [x] Fix sonrası rollback
- [x] Project/file hash ile stale-change kontrolü
- [x] Local mutation/restore audit log — Apply, restore ve Flowchart dönüşüm işlemleri proje içindeki `.rpadevassistant/logs` altında append-only JSONL kayıtları ve operation/backup kimlikleriyle izlenir.
- [x] Safe Automatic Fix sınıflandırması
- [x] DisplayName otomatik düzeltme
- [x] Workflow rename preview — RPA006 önerisi eski/yeni relative path diff’ini, statik olarak çözümlenen caller workflow’ları ve güncellenmesi gereken Invoke Workflow File referanslarını gösterir; dynamic/external referansları açıkça manuel doğrulamaya bırakır ve dosya değiştirmez.
- [ ] Workflow rename sırasında Invoke Workflow referanslarının güncellenmesi
- [x] Flowchart → Sequence dönüşüm preview — Workflow detail ve standalone XAML seçimi için doğrulandı.
- [x] Flowchart → Sequence kontrollü dönüşüm — Safe conversion apply, stale hash kontrolü, backup/rollback ve standalone Save As akışıyla doğrulandı.
- [x] Refactoring önerileri — RPA025 için parsed activity, nesting, decision, loop ve argument metriklerinden açıklanabilir decomposition planı üretilir; explicit argument contract, exception/retry sınırları ve adım adım regression doğrulaması önerilir, business boundary uydurulmaz ve auto-apply yapılmaz.

## 6. Localization & UX

- [x] Ortak uygulama shell’i ve ana çalışma ekranlarının kompakt desktop dashboard tasarım diline uyarlanması
- [x] Rules ekranının metrikler, filtreler, katalog tablosu, seçili rule detayı ve custom rule builder ile yoğun desktop katalog düzenine getirilmesi
- [x] Türkçe / İngilizce dil desteği
- [x] Bulguların Türkçeleştirilmesi
- [x] Recommendation metinlerinin Türkçeleştirilmesi
- [x] Fix Suggestion metinlerinin Türkçeleştirilmesi
- [x] AI Review çıktılarının seçili dile göre üretilmesi
- [x] Workflow sayısı kartının tıklanabilir olması
- [x] Finding sayısı kartlarının tıklanabilir olması
- [x] Workflow arama
- [x] Workflow filtreleme
- [x] Finding filtreleme
- [x] Finding arama
- [x] Finding severity sıralaması — Findings ekranında varsayılan sıra ile yüksekten düşüğe/düşükten yükseğe severity sıralama kontrolü eklendi.
- [x] Workflow risk göstergesi
- [ ] Activity tree içerisinde arama
- [x] Büyük projelerde performans iyileştirmeleri — Workflow/finding türetilmiş verileri cache'lendi, workflow finding sayımı tek geçişe indirildi, Findings kademeli ve Activity Tree açılan dal bazlı render ediliyor. Backend artık workflow keşfi, XAML parse, scan, rule analysis, scoring ve toplam süreleri raporluyor; 51 workflow/6.228 activity içeren gerçek E-Haciz proje kopyasında 5 sıcak koşunun medyan toplam süresi 307,7 ms olarak ölçüldü.

## 7. Reports

- [x] HTML rapor geliştirmeleri
- [x] PDF rapor export
- [x] Yönetici özeti — Canonical rapor modelinde risk düzeyi, Critical/Error toplamı, etkilenen workflow sayısı, en çok etkilenen workflow ve öncelikli Rule ID’ler üretilir; HTML/PDF çıktılarında TR/EN narrative yönetici özeti gösterilir.
- [x] Teknik detay raporu
- [x] Workflow bazlı rapor
- [x] Rule bazlı rapor
- [ ] Şirket standardı compliance raporu
- [ ] Önceki analizle karşılaştırmalı rapor
- [ ] AI Review’un rapora eklenmesi
- [ ] Fix önerilerinin rapora eklenmesi
- [ ] Report branding / şirket logosu

## 8. Rule Profiles

- [x] Custom Rule Profiles
- [x] Local rule/profile yönetimi — Single-user desktop kullanımında custom rule/profile oluşturma, proje-profile eşleme, import/export ve default profile overlay desteklenir.
- [x] Şirket standartlarına özel rule set
- [ ] Strict profile
- [ ] Legacy profile
- [ ] REFramework profile
- [ ] Modern UiPath profile
- [ ] Migration profile
- [x] Kullanıcı tarafından rule enable/disable
- [x] Rule severity özelleştirme
- [x] Rule weight özelleştirme
- [x] Rule max penalty özelleştirme
- [x] Şirket bazlı custom rule ekleme

## 9. Analysis History

- [x] Analysis history
- [x] Önceki analizle karşılaştırma
- [x] Skor değişim geçmişi
- [x] Yeni finding tespiti
- [x] Kapanan finding tespiti
- [x] Değişmeyen finding tespiti
- [x] Workflow bazlı değişim geçmişi
- [ ] Trend analizi

## 10. UiPath Integration

- [ ] UiPath Studio entegrasyonu
- [ ] UiPath Studio’dan RPA Dev Assistant açma
- [ ] UiPath Studio’da seçili workflow’u analiz etme
- [ ] Orchestrator entegrasyonu
- [ ] Orchestrator process analizi
- [ ] Queue analizi
- [ ] Asset analizi
- [ ] Robot / Machine konfigürasyon analizi
- [ ] Orchestrator API entegrasyonu
- [ ] Automation Suite desteği

## 11. Git & CI/CD

- [ ] GitHub entegrasyonu
- [ ] GitLab entegrasyonu
- [ ] Azure DevOps entegrasyonu
- [ ] Pull Request code review
- [ ] Commit bazlı analiz
- [ ] Branch karşılaştırması
- [ ] CI/CD quality gate
- [ ] Minimum score threshold
- [ ] Error/Critical finding varsa pipeline fail
- [ ] Review sonuçlarını PR comment olarak yazma

## 12. Company Internal Version

- [ ] Şirket içi merkezi kullanım
- [ ] Kullanıcı yönetimi
- [ ] Rol ve yetki yönetimi
- [ ] SSO
- [ ] Merkezi/kurumsal audit log
- [ ] Merkezi çok kullanıcılı rule/profile governance
- [ ] Merkezi analysis history
- [ ] Takım bazlı projeler
- [ ] Şirket dashboard’u
- [ ] Proje kalite trendleri
- [ ] Developer/team bazlı kalite metrikleri
- [ ] On-premise deployment

## 13. Enterprise / Customer Version

- [ ] Multi-tenant yapı
- [ ] Müşteri bazlı izolasyon
- [ ] Müşteri bazlı rule profile
- [ ] Müşteri bazlı branding
- [ ] Lisanslama
- [ ] Subscription modeli
- [ ] Kullanım kotası
- [ ] Enterprise SSO
- [ ] Audit / compliance
- [ ] Customer admin panel
- [ ] On-premise seçeneği
- [ ] SaaS seçeneği
- [ ] Private AI provider desteği

## 14. Product Platform

- [ ] Auto-update
- [ ] Telemetry / kullanım istatistikleri
- [ ] Crash reporting
- [ ] Plugin/modül sistemi
- [ ] Feature flag sistemi
- [x] Configuration management — Local desktop runtime için CORS allowlist, custom rule/profile storage, analysis history retention/root ve resmî package metadata endpoint/cache ayarları tek typed ve startup’ta doğrulanan configuration contract’ında merkezileştirildi; environment override ve güvenli varsayılanlar desteklenir, secret değerler loglanmaz.
- [x] Desktop installer iyileştirmeleri — Windows 2022 CI üzerinde backend/frontend validation, win-x64 sidecar publish, Tauri NSIS build, non-empty artifact doğrulama, artifact retention ve sertifika secret’ları sağlandığında timestamp’li installer signing akışı eklendi; stabil bundle identifier upgrade continuity’sini korur.
- [ ] Code signing
- [ ] Windows installer — Bloke: NSIS build ve Windows 2022 CI pipeline’ı hazır; workflow henüz uzak repository’ye gönderilmedi ve yerel Parallels lisansı sona erdiği için üretilen installer’ın Windows 10/11 kurulum acceptance testi yapılamadı.
- [x] macOS desteği — macOS üzerinde Tauri desktop uygulaması, backend sidecar ve dev ortamı çalışır durumda doğrulandı.
- [ ] Linux desteği

## 15. Variable & Argument Intelligence

- [x] XAML parser'da workflow argument adı, type ve direction bilgisinin çıkarılması
- [x] XAML parser'da variable adı, type, default value ve scope bilgisinin çıkarılması
- [x] Static Invoke Workflow File argument mapping key/value bilgilerinin parser modeline aktarılması
- [x] Argument/variable kullanımlarını expression'lardan güvenilir ve case-insensitive çözümleme — Wrapper/member expression token çözümlemesi ve stable scope `IdRef` üzerinden nested variable shadowing analizi tamamlandı; locator bulunmadığında fail-safe davranır.
- [x] Naming convention kurallarının profile/proje bazlı yapılandırılabilmesi — RPA006 workflow pattern/prefix, RPA031 In/Out/InOut prefix ve RPA032 variable pattern/prefix ayarlarını seçili rule profile’dan okur; proje-profile eşlemesiyle proje bazında uygulanır ve geçersiz regex güvenli varsayılana döner.
- [x] Argument direction kontrolü (In / Out / InOut) — RPA038 parsed expression access'ini read/write olarak sınıflandırır; yazılan In ve yalnız okunan Out argument'leri workflow-level finding olarak raporlar.
- [x] Gereksiz InOut argument tespiti — RPA039 yalnız okunan InOut için In, yalnız yazılan InOut için Out direction önerir; read/write ve kullanılmayan argument'lerde duplicate finding üretmez.
- [x] Argument type kontrolü — RPA040 eksik veya hatalı `InArgument(...)`, `OutArgument(...)` ve `InOutArgument(...)` type declaration'larını workflow-level olarak raporlar; nested generic type ifadelerini destekler.
- [ ] Argument default value kontrolü
- [x] Argument naming convention kontrolü
- [x] Variable naming convention kontrolü
- [x] Kullanılmayan variable tespiti — RPA033 doğrudan expression referanslarını ve stable scope `IdRef` bulunan nested/shadowed variable kullanımlarını case-insensitive çözümler; belirsiz scope'larda false-positive üretmez.
- [x] Kullanılmayan argument tespiti — RPA034 doğrudan expression referanslarını, case-insensitive static Invoke mapping key'lerini, caller-relative path'leri ve runtime `ArgumentsVariable` sözleşmelerini denetler; çözümlenemeyen dinamik çağrılarda false-positive üretmez.
- [x] Gereğinden geniş variable scope tespiti — RPA041 yalnız stable activity ancestry ile doğrulanan ve tüm referansları daha dar bir Sequence, Flowchart veya StateMachine içinde kalan variable'ları raporlar.
- [x] Aynı isimli shadowed variable tespiti — RPA042 yalnız ancestor/descendant scope ilişkisi kesin olan case-insensitive aynı adlı nested variable declaration'larını raporlar; sibling ve locator'sız scope'larda fail-safe davranır.
- [x] Invoke Workflow argument mapping doğrulaması — RPA043 yalnız statik ve tekil olarak çözümlenebilen callee workflow contract'ında bulunmayan mapping key'lerini raporlar.
- [x] Eksik Invoke Workflow argument mapping tespiti — RPA044 map edilmemiş, type declaration ile yönü doğrulanmış ve serialized default değeri bulunmayan In/InOut argument'leri Suggestion olarak gösterir; default'lu input'ları, Out argument'leri, runtime dictionary mapping'lerini ve dynamic invoke çağrılarını dışarıda bırakır.
- [x] Yanlış direction ile map edilen argument tespiti — Parser mapping wrapper direction bilgisini korur; RPA045 bunu hedef workflow declaration direction'ı ile karşılaştırır.
- [ ] Workflow argument contract görünümü

---

## Yeni Tespitler

> Codex repository incelemesi sırasında bu listede olmayan fakat eklenmesi gereken teknik borçları veya ürün özelliklerini buraya ekleyebilir.

- [ ] Repo kökünde istenen `PRODUCT_BACKLOG.md` yok; mevcut backlog dosyası `RPA_Dev_Assistant_Product_Backlog.md` adıyla duruyor.
- [x] Frontend tek dosyada büyümüş durumda; dashboard/settings/workflows/fix/AI ekranları component dosyalarına ayrılmalı — `main.tsx` 13 bağımsız bileşene (`components/`) ve modüler CSS dosyalarına ayrıldı.
- [x] AI Review ve Ask Project cevap dili seçili locale’a göre garanti edilmiyor; prompt/request seviyesinde locale bağlandı ve TR/EN acceptance ile doğrulandı.
- [x] RPA015 gerçek projede ambiguous finding üretiyor; default profile’da kapatıldı, rule catalog’da opsiyonel/custom profile ile açılabilir halde bırakıldı.
- [x] Findings ekranında ayrı RuleId/rule dropdown filtresi eklendi; RuleId filtresi category/severity/search ile birlikte regression testleriyle doğrulandı.
- [x] Backend-generated Fix Suggestion title/description/steps/risks için locale-aware contract yok; backend fix suggestion metinleri TR/EN locale ile doğrulandı.
- [x] Aktif Rule’lar ekranındaki şablon filtresi kaldırıldı; Profile Şablonu dropdown’ı aktif rule ekranına eklendi ve kayıt sonrası popup bağımsız toast state’iyle görünür kaldı.
- [x] Flowchart dönüşümü sırasında CommentOut, Comment ve XML yorum bloklarının dönüştürülen Sequence'e taşınması engellendi; Step 3 Preview & Save alanı görsel akış kartları, kullanılacak aktiviteler tablosu ve formatlanmış XAML kod önizleme bileşenleriyle zenginleştirildi.
- [x] Flowchart içinde custom dependency aktivitelerinin tespiti, standart UiPath aktivite önerileri ve kullanıcı onayıyla XAML üzerinde otomatik değiştirilmesi özelliği eklendi.
- [x] Flowchart analizinde ve dönüşümünde özel/transitif bağımlılıkların (ör. Ucgen.CustomLibrary altından gelen UiPath.WebAPI.Activities) tespiti yapıldı; HttpClient aktivitelerinin XML elemanları (EndPoint, Method, Result, StatusCode, Headers, Parameters, Attachments, Cookies) ve namespace tanımları eksiksiz korunacak şekilde XAML çevirici güncellendi, iç container'ların (Dictionary vb.) aktivite ismi sanılması engellendi ve eksik doğrudan paket bağımlılıklarının project.json'a eklenmesi sağlandı.
- [x] Flowchart XML `<Flowchart.StartNode>` alt eleman ve attribute çözümlemesi düzeltildi; başlangıç aktivitelerinin (ör. `retry_count = 1`) Sequence'in en başına yerleştirilmesi sağlandı.
- [x] Flowchart değişkenleri (`<Flowchart.Variables>`), üretilen Sequence köküne (`<Sequence.Variables>`) aktarılarak dönüştürülen akışta değişken tanımlarının (`retry_count`, `json_sorgu`, `result_sorgu`, `Durum`, `get_status` vb.) eksiksiz tanımlanması sağlandı.
- [x] CommentOut ve yorum bloklarının dönüştürülen Sequence içine taşınması XAML klonlama ve Sequence oluşturma aşamalarında derinlemesine temizlenerek tamamen engellendi.
- [x] `DeserializeJson`, `HttpClient` gibi aktiviteler için `UiPath.WebAPI.Activities` doğrudan bağımlılığı `project.json` dosyasına eklendi ve `njl:JObject` namespace çözümlemeleri korundu.

---

## İnceleme Özeti

**Son inceleme tarihi:** 2026-09-17
**İnceleyen:** Codex
**Tamamlanan madde sayısı:** 149
**Kısmi madde sayısı:** 0
**Bekleyen madde sayısı:** 75

### Notlar

- `PRODUCT_BACKLOG.md` dosyası bulunmadığı için bu inceleme repository’deki mevcut backlog kaynağı olan `RPA_Dev_Assistant_Product_Backlog.md` üzerinde yapıldı.
- Tamamlandı işaretleri kod, test, endpoint, frontend entegrasyonu ve README kanıtlarıyla sınırlı tutuldu.
