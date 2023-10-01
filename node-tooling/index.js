#!/usr/bin/env node

const https = require("https");
const http = require("http");
const fs = require("fs");
const path = require("path");

const [command, rpcUrl, outputFilePath, customPrimitivesModule] = [
  process.argv[2],
  process.argv[3],
  process.argv[4],
  process.argv[5],
];

function printSyntax() {
  console.info(
    "Syntax: cooke-rpc generate <http endpoint> <output file> [import/path/to/custom/primitives.ts]"
  );
}

if (command !== "generate") {
  console.error("Unknown command.");
  printSyntax();
  return;
}

if (!rpcUrl || !outputFilePath) {
  console.error("Incorrect input paramters.");
  printSyntax();
  return;
}

const outputDir = path.dirname(outputFilePath);
if (!fs.existsSync(outputDir)) {
  fs.mkdirSync(outputDir);
}

(rpcUrl.startsWith("https") ? https : http)
  .request(rpcUrl + "/introspection", (res) => {
    let data = "";
    res.on("data", (d) => (data += d));

    res.on("end", () => {
      const meta = JSON.parse(data);
      generateRpcTs(meta);
    });
  })
  .on("error", console.error)
  .end();

function generateRpcTs(meta) {
  const stream = fs.createWriteStream(outputFilePath, {
    flags: "w",
  });

  stream.write("// tslint:disable\n");

  stream.write(`import { createRpcInvoker } from "cooke-rpc";\n\n`);

  const nativePrimitives = [
    "string",
    "array",
    "boolean",
    "number",
    "optional",
    "void",
  ];

  if (customPrimitivesModule) {
    for (const type of meta.types) {
      if (type.kind === "primitive" && !nativePrimitives.includes(type.name)) {
        stream.write(
          `import { ${type.name} } from "${customPrimitivesModule}";\n`
        );
      }
    }

    stream.write(`\n`);
  }

  // Register all types that requires a discriminator due to participating in polymorphic scenarios
  const discriminatedTypes = new Set();
  for (const type of meta.types) {
    if (type.kind === "union") {
      for (const memberType of type.types) {
        discriminatedTypes.add(memberType.name);
      }
    } else if (type.kind === "object") {
      if (type.extends && type.extends.length > 0) {
        discriminatedTypes.add(type.name);
        for (const extendedType of type.extends) {
          discriminatedTypes.add(
            typeof extendedType === "object" ? extendedType.name : extendedType
          );
        }
      }
    }
  }

  var typesByName = new Map();
  for (const type of meta.types) {
    typesByName.set(type.name, type);
  }

  function isAbstract(typeName) {
    return !!typesByName.get(typeName)?.abstract;
  }

  function isBase(typeName) {
    var type = typesByName.get(typeName);
    if (type.kind !== "object") {
      return false;
    }

    var extenders = meta.types.filter(
      (x) =>
        x.extends &&
        (x.extends.includes(typeName) ||
          x.extends.map((x) => x.name).includes(typeName))
    );

    return extenders.length > 0;
  }

  function writeProperties(stream, properties) {
    if (!properties) {
      return;
    }

    stream.write(
      properties
        .map(
          (p) =>
            `  ${p.name}${
              (p.type.kind === "generic" && p.type.name === "optional") ||
              p.optional
                ? "?"
                : ""
            }: ${formatTypeUsage(p.type)};`
        )
        .join("\n")
    );
  }

  function writeObject(stream, type, name) {
    stream.write(`export interface ${name} `);

    if (type.typeParameters && type.typeParameters.length > 0) {
      stream.write("<");
      stream.write(type.typeParameters.join(", "));
      stream.write("> ");
    }

    const actualExtends = type.extends?.filter((x) =>
      typeof x === "object" ? isBase(x.name) : isBase(x)
    );
    if (actualExtends && actualExtends.length > 0) {
      stream.write("extends ");
      stream.write(
        actualExtends
          .map((x) =>
            typeof x === "object"
              ? `${formatExtendType(x.name)}<${x.typeArguments.join(", ")}>`
              : formatExtendType(x)
          )

          .join(", ")
      );
      stream.write(" ");
    }

    stream.write("{\n");
    if (!type.abstract) {
      if (discriminatedTypes.has(type.name)) {
        stream.write(`  $type: "${type.name}";\n`);
      } else {
        stream.write(`  $type?: "${type.name}";\n`);
      }
    }

    writeProperties(stream, type.properties);
    stream.write("\n}");

    function formatExtendType(typeName) {
      const output = `${typeName}${isBase(typeName) ? "$Base" : ""}`;
      if (!isAbstract(typeName)) {
        return `Omit<${output}, "$type">`;
      }

      return output;
    }
  }

  for (const type of meta.types) {
    if (type.kind === "primitive") {
    } else if (type.kind === "union") {
      stream.write(`export type ${type.name} = `);
      stream.write(formatTypeUsage(type));
      stream.write(";\n\n");
    } else if (type.kind === "object") {
      if (isBase(type.name)) {
        writeObject(stream, type, type.name + "$Base");
        stream.write("\n\n");

        var extenders = meta.types.filter(
          (x) =>
            x.extends &&
            (x.extends.includes(type.name) ||
              x.extends.map((x) => x.name).includes(type.name))
        );
        stream.write(`export type ${type.name}`);
        if (type.typeParameters && type.typeParameters.length > 0) {
          stream.write("<");
          stream.write(type.typeParameters.join(", "));
          stream.write(">");
        }
        stream.write(` = `);

        stream.write(
          extenders
            .concat(
              !type.abstract ? [{ ...type, name: type.name + "$Base" }] : []
            )
            .map((x) => formatTypeUsage(x))
            .join(" | ")
        );
        stream.write(";\n\n");
      } else {
        writeObject(stream, type, type.name);
        stream.write("\n\n");
      }
    } else if (type.kind === "enum") {
      stream.write(`export enum ${type.name} {\n`);
      stream.write(
        type.members
          .map(
            (x) =>
              `  ${x.name.match(/[^\w]/) ? `"${x.name}"` : x.name} = "${
                x.name
              }"`
          )
          .join(",\n")
      );
      stream.write("\n}\n\n");
    } else {
      throw new Error(`Unknown RPC type kind: ${type.kind}`);
    }
  }

  for (const service of meta.services) {
    stream.write(`export const ${toCamelCase(service.name)} = {\n`);

    for (const proc of service.procedures) {
      const argsType = `[${proc.parameters
        .map((p) => `${p.name}: ${formatTypeUsage(p.type)}`)
        .join(", ")}]`;
      stream.write(
        `  ${toCamelCase(proc.name)}: createRpcInvoker<'${service.name}', '${
          proc.name
        }', ${argsType}, ${formatTypeUsage(proc.returnType)}>('${
          service.name
        }', '${proc.name}'),\n`
      );
    }

    stream.write(`}\n\n`);
  }

  stream.end();
}

function formatTypeUsage(type) {
  if (typeof type === "string") {
    return type;
  }

  switch (type.kind) {
    case "union":
      if (type.types.length === 0) {
        return "never";
      }

      return type.types.map(formatTypeUsage).join(" | ");

    case "generic":
      switch (type.name) {
        case "array":
          return `Array<${type.typeArguments.map(formatTypeUsage).join(",")}>`;

        case "tuple":
          return `[${type.typeArguments.map(formatTypeUsage).join(",")}]`;

        case "optional":
          return `${type.typeArguments
            .map(formatTypeUsage)
            .join(",")} | undefined`;

        case "map": {
          const keyType = formatTypeUsage(type.typeArguments[0]);
          const valueType = formatTypeUsage(type.typeArguments[1]);
          if (keyType === "string" || keyType === "number") {
            return `{[key: ${keyType}]: ${valueType}}`;
          } else {
            return `{[key in ${keyType}]?: ${valueType}}`;
          }
        }

        default:
          return `${type.name}<${type.typeArguments
            .map(formatTypeUsage)
            .join(",")}>`;
      }

    case "object":
      if (type.typeParameters && type.typeParameters.length > 0) {
        return `${type.name}<${type.typeParameters
          .map(formatTypeUsage)
          .join(",")}>`;
      }

      return type.name;

    default:
      return type.name;
  }
}

function toCamelCase(str) {
  if (str[0] !== str[0].toLowerCase()) {
    return str[0].toLowerCase() + str.substr(1);
  }

  return str;
}
