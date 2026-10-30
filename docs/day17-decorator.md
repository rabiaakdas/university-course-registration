# Gün 17 — Decorator ve davranış sırası

Decorator, sardığı nesneyle aynı sözleşmeyi sunup çağrının çevresine davranış ekler.
`Application/IAcademicEligibilityEvaluator.cs` gerçek hazırlanmış girdi ve
AcademicEvaluation sonucunu kullanır. Strategy alternatif hesaplama politikasını
seçiyordu; burada hesaplama politikası değişmeden gözlem eklenir.

`TimingAcademicEligibilityEvaluator.Evaluate` yerel Stopwatch açar, iç Evaluate'ı
bir kez çağırır ve aynı sonuç nesnesini döndürür. finally başarı, akademik ret ve
exception yolunda çalışır; catch ile hata dönüştürülmez, özgün exception/stack
ilerler. Sonuç yokken `FailureCount=null`, `Outcome=failed`; uygun değilse
`ineligible` olur. Süre finite ve negatif olmayan sayıdır; kesin ms beklenmez.
Kişisel veriler, başarı listesi, request veya bağlantı loglanmaz. Log sağlayıcısının
throw etmesi finally'deki asıl sonucu/hata akışını bozabilir. Bu sınır için retry,
fallback veya hata yutma altyapısı eklenmedi.

`EnrollmentOperations.EvaluateAsync` toplu DB sorgularını önce tamamlar. Ölçüm
sadece son evaluator çağrısındadır; veri hazırlama, yazma ve HTTP dışarıdadır.
Değerlendirme senkron kaldığı için Task.Run/FromResult/async zinciri eklenmedi.

## Composition ve DI

Program somut `AcademicEligibility` kaydını scoped sağlar. Interface kaydı
`TimingAcademicEligibilityEvaluator(somut evaluator, ILogger)` oluşturur.
Decorator'ı oluştururken aynı interface'i tekrar GetRequiredService ile çözmek
`interface → decorator → interface` döngüsü doğururdu. Concrete kaydı çözmek
bu döngüyü önler. IServiceProvider yalnız composition root'ta kullanılır.
Scoped zincir DbContext yaşam süresiyle uyumludur; singleton içine scoped nesne
alınmaz. Saf testler doğrudan AcademicEligibility oluşturabilir.

## Üretim dışı deneyler

`scripts/DecoratorChecks.cs` içindeki DecoratorExperiments:

- AsyncTiming aynı CancellationToken'ı geçirir ve iç Task'ı **await** eder. finally
  tamamlanma/hata/iptal sonrasında çalışır. Task'ı await etmeden dönmek yalnız
  başlatma süresini ölçerdi. TaskCompletionSource kontrolü, işlem beklerken ölçüm
  olmadığını; sonuç/aynı exception/canceled Task ve token'ın korunduğunu gösterir.
- Timing(Logging(Actual)): timing:start, logging:start, actual, logging:end,
  timing:end. Logging(Timing(Actual)) bunun ters iç içe sırasıdır. Her iki wrapper'ın
  end davranışı finally'dir, success-only değildir. Hata yolunda da aynı kapanış
  sırası ve tek actual çağrısı korunur. Dış timing logging'i de kapsar, iç timing
  yalnız kendi içindeki davranışı kapsar; sıra ölçüm sınırını değiştirir.

Bu kod web projesinin `Compile Remove="scripts/**/*.cs"` sınırı dışındadır.
Üretime ikinci decorator veya asenkron evaluator eklenmedi.

## Kanıt sınırı

CapturedLog test ILogger'ı event kimliğini ve alanları inceler, mesaj cümlesini
karşılaştırmaz. EvaluationLogObserver gerçek API'nin JSON konsol akışında yalnız
ölçüm alanlarını tutar; RequestFinished olayını sıra bariyeri olarak bekler.
Sabit süre uyutmayı başarı kanıtı saymaz; polling sınırlı süre sonunda başarısızdır.
HTTP cevaplarına ölçüm alanı eklenmez. İzole testte geçen sonuçlar benchmark,
kontenjan veya eşzamanlı kredi/program garantisi değildir.

Kaynaklar: [Microsoft yapılandırılmış logging/EventId](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging/overview),
[Stopwatch](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch).
