<?xml version="1.0" encoding="utf-8"?>
<!--
  Runs on class-parse output (api.xml.class-parse), before the binding generator
  resolves types.

  Kotlin `internal` interfaces show up in class-parse output as non-public. When a
  public class implements one (io.livekit.android.room.Room implements the internal
  RTCEngine.Listener), the generator can't resolve the interface and silently drops
  the whole public class. Metadata.xml runs too late to prevent that.

  This removes only the <implements> entries that point at non-public types, so the
  public class survives. The class's own public methods are unaffected.
-->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output method="xml" indent="no" encoding="utf-8" />

  <!-- Every declared type, keyed by fully-qualified Java name ("pkg.Outer.Inner"). -->
  <xsl:key name="type" match="/api/package/class | /api/package/interface"
           use="concat(../@name, '.', @name)" />

  <!-- Identity copy. -->
  <xsl:template match="@* | node()">
    <xsl:copy>
      <xsl:apply-templates select="@* | node()" />
    </xsl:copy>
  </xsl:template>

  <!-- Drop implements of types declared in these jars that aren't public/protected. -->
  <xsl:template match="implements[key('type', @name)[@visibility != 'public' and @visibility != 'protected']]" />
</xsl:stylesheet>
